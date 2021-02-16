/*
 * Apache License, Version 2.0
 * Copyright 2019-2021 NVIDIA Corporation
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Microsoft.Extensions.Logging;
using Nvidia.Clara.DicomAdapter.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FoDicom = Dicom;
using FoDicomLog = Dicom.Log;
using FoDicomNetwork = Dicom.Network;

namespace Nvidia.Clara.DicomAdapter.Server.Services.Scp
{
    /// <summary>
    /// A new instance of <c>ScpServiceInternal</c> is created for every new association.
    /// </summary>
    internal class ScpServiceInternal :
        FoDicomNetwork.DicomService,
        FoDicomNetwork.IDicomServiceProvider,
        FoDicomNetwork.IDicomCEchoProvider,
        FoDicomNetwork.IDicomCStoreProvider
    {
        private ILogger<ScpServiceInternal> _logger;
        private IApplicationEntityManager _associationDataProvider;
        private IDisposable _loggerScope;
        private uint _associationId;
        private string _associationIdStr;

        public ScpServiceInternal(FoDicomNetwork.INetworkStream stream, Encoding fallbackEncoding, FoDicomLog.Logger log)
            : base(stream, fallbackEncoding, log)
        {
        }

        public FoDicomNetwork.DicomCEchoResponse OnCEchoRequest(FoDicomNetwork.DicomCEchoRequest request)
        {
            _logger?.Log(LogLevel.Information, $"C-ECH request received");
            return new FoDicomNetwork.DicomCEchoResponse(request, FoDicomNetwork.DicomStatus.Success);
        }

        public void OnConnectionClosed(Exception exception)
        {
            if (exception != null)
            {
                _logger?.Log(LogLevel.Error, "Connection closed with exception: {0}", exception);
            }

            if (ScpService.ConnectionClosed != null)
            {
                ScpService.ConnectionClosed(null, _associationId);
            }

            _loggerScope?.Dispose();
            Interlocked.Decrement(ref ScpService.ActiveConnections);
        }

        public FoDicomNetwork.DicomCStoreResponse OnCStoreRequest(FoDicomNetwork.DicomCStoreRequest request)
        {
            try
            {
                _logger?.Log(LogLevel.Information, "Transfer syntax used: {0}", request.TransferSyntax);
                _associationDataProvider.HandleCStoreRequest(request, Association.CalledAE, _associationId);
                return new FoDicomNetwork.DicomCStoreResponse(request, FoDicomNetwork.DicomStatus.Success);
            }
            catch (System.IO.IOException ex) when ((ex.HResult & 0xFFFF) == 0x27 || (ex.HResult & 0xFFFF) == 0x70)
            {
                _logger?.Log(LogLevel.Error, "Failed to process C-STORE request, out of storage space: {ex}", ex);
                return new FoDicomNetwork.DicomCStoreResponse(request, FoDicomNetwork.DicomStatus.ResourceLimitation);
            }
            catch (Exception ex)
            {
                _logger?.Log(LogLevel.Error, "Failed to process C-STORE request: {ex}", ex);
                return new FoDicomNetwork.DicomCStoreResponse(request, FoDicomNetwork.DicomStatus.ProcessingFailure);
            }
        }

        public void OnCStoreRequestException(string tempFileName, Exception e)
        {
            _logger?.Log(LogLevel.Error, e, "Exception handling C-STORE Request");
        }

        public void OnReceiveAbort(FoDicomNetwork.DicomAbortSource source, FoDicomNetwork.DicomAbortReason reason)
        {
            _logger?.Log(LogLevel.Warning, "Aborted {0} with reason {1}", source, reason);
        }

        /// <summary>
        /// Start timer only if a receive association release request is received.
        /// </summary>
        /// <returns></returns>
        public Task OnReceiveAssociationReleaseRequestAsync()
        {
            _logger?.Log(LogLevel.Information, "Association release request received");
            return SendAssociationReleaseResponseAsync();
        }

        public Task OnReceiveAssociationRequestAsync(FoDicomNetwork.DicomAssociation association)
        {
            Interlocked.Increment(ref ScpService.ActiveConnections);
            _associationDataProvider = UserState as IApplicationEntityManager;

            if (_associationDataProvider is null)
            {
                throw new ArgumentNullException("userState must be an instance of IAssociationDataProvider");
            }

            _logger = _associationDataProvider.GetLogger<ScpServiceInternal>(Association.CalledAE);

            _associationId = _associationDataProvider.NextAssociationNumber();
            _associationIdStr = $"#{_associationId} {association.RemoteHost}:{association.RemotePort}";

            _loggerScope = _logger?.BeginScope(new LogginDataDictionary<string, object> { { "Association", _associationIdStr } });
            _logger?.Log(LogLevel.Information, "Association received from {0}:{1}", association.RemoteHost, association.RemotePort);

            if (!IsValidSourceAe(association.CallingAE, association.RemoteHost))
            {
                return SendAssociationRejectAsync(
                    FoDicomNetwork.DicomRejectResult.Permanent,
                    FoDicomNetwork.DicomRejectSource.ServiceUser,
                    FoDicomNetwork.DicomRejectReason.CallingAENotRecognized);
            }

            if (!IsValidCalledAe(association.CalledAE))
            {
                return SendAssociationRejectAsync(
                    FoDicomNetwork.DicomRejectResult.Permanent,
                    FoDicomNetwork.DicomRejectSource.ServiceUser,
                    FoDicomNetwork.DicomRejectReason.CalledAENotRecognized);
            }

            foreach (var pc in association.PresentationContexts)
            {
                if (pc.AbstractSyntax == FoDicom.DicomUID.Verification)
                {
                    if (!_associationDataProvider.Configuration.Value.Dicom.Scp.Verification.Enabled)
                    {
                        _logger?.Log(LogLevel.Warning, "Verification service is disabled: rejecting association");
                        return SendAssociationRejectAsync(
                            FoDicomNetwork.DicomRejectResult.Permanent,
                            FoDicomNetwork.DicomRejectSource.ServiceUser,
                            FoDicomNetwork.DicomRejectReason.ApplicationContextNotSupported
                        );
                    }
                    pc.AcceptTransferSyntaxes(_associationDataProvider.Configuration.Value.Dicom.Scp.Verification.TransferSyntaxes.ToDicomTransferSyntaxArray());
                }
                else if (pc.AbstractSyntax.StorageCategory != FoDicom.DicomStorageCategory.None)
                {
                    // Accept any proposed TS
                    pc.AcceptTransferSyntaxes(pc.GetTransferSyntaxes().ToArray());
                }
            }

            return SendAssociationAcceptAsync(association);
        }

        private bool IsValidCalledAe(string calledAe)
        {
            return _associationDataProvider.IsAeTitleConfigured(calledAe);
        }

        private bool IsValidSourceAe(string callingAe, string host)
        {
            if (!_associationDataProvider.Configuration.Value.Dicom.Scp.RejectUnknownSources) return true;

            var task = Task.Run(async () => await _associationDataProvider.IsValidSource(callingAe, host));
            task.Wait();
            return task.Result;
        }
    }
}