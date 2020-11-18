using Microsoft.Extensions.Logging;
using Nvidia.Clara.DicomAdapter.DicomWeb.Client;
using Nvidia.Clara.DicomAdapter.DicomWeb.Client.API;
using System;
using System.Net.Http.Headers;

namespace Nvidia.Clara.DicomAdapter.Server.Repositories
{
    public interface IDicomWebClientFactory
    {
        IDicomWebClient CreateDicomWebClient(
            Uri uriRoot,
            AuthenticationHeaderValue credentials,
            string wadoUrlPrefix,
            string qidoUrlPrefix,
            string stowUrlPrefix,
            string deleteUrlPrefix);
    }

    internal class DicomWebClientFactory : IDicomWebClientFactory
    {
        private readonly ILoggerFactory _loggerFactory;

        public DicomWebClientFactory(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        public IDicomWebClient CreateDicomWebClient(Uri uriRoot, AuthenticationHeaderValue credentials, string wadoUrlPrefix, string qidoUrlPrefix, string stowUrlPrefix, string deleteUrlPrefix)
        {
            return new DicomWebClient(
                uriRoot,
                credentials,
                wadoUrlPrefix,
                qidoUrlPrefix,
                stowUrlPrefix,
                deleteUrlPrefix,
                _loggerFactory.CreateLogger("DicomWebClient"));
        }
    }
}