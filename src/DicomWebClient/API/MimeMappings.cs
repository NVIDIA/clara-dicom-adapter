/*
 * Apache License, Version 2.0
 * Copyright 2019-2020 NVIDIA Corporation
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

using Dicom;
using System.Collections.Generic;
using System.Net.Http.Headers;

namespace Nvidia.Clara.Dicom.DicomWeb.Client.API
{
    public enum MimeType : short
    {
        Dicom = 0,
        DicomJson = 1,
        DicomXml = 2,
        OctetStreme = 3,
        ImageJpeg = 10,
        ImageGif = 11,
        ImagePng = 12,
        ImageJp2 = 13,
        ImageJpx = 14,
        VideoMpeg = 20,
        VideoMp4 = 21,
        VideoH265 = 22,
        VideoMpeg2 = 23
    }

    public static class MimeMappings
    {
        public const string MultiPartRelated = "multipart/related";

        public static readonly Dictionary<MimeType, string> MimeTypeMappings = new Dictionary<MimeType, string>()
        {
            { MimeType.Dicom, "application/dicom" },
            { MimeType.DicomJson, "application/dicom+json" },
            { MimeType.DicomXml, "application/dicom+xml" },
            { MimeType.OctetStreme, "application/octet-stream" },
            { MimeType.ImageJpeg, "image/jpeg" },
            { MimeType.ImageGif, "image/gif" },
            { MimeType.ImagePng, "image/png" },
            { MimeType.ImageJp2, "image/jp2" },
            { MimeType.ImageJpx, "image/jpx" },
            { MimeType.VideoMpeg, "video/mpeg" },
            { MimeType.VideoMp4, "video/mp4" },
            { MimeType.VideoH265, "video/H265" },
            { MimeType.VideoMpeg2, "video/mpeg2" },
        };

        public static readonly Dictionary<DicomUID, MimeType> SupportedMediaTypes = new Dictionary<DicomUID, MimeType>()
        {
            { DicomUID.ExplicitVRLittleEndian, MimeType.Dicom },
            { DicomUID.RLELossless, MimeType.Dicom },
            { DicomUID.JPEGBaseline1, MimeType.Dicom },
            { DicomUID.JPEGExtended24, MimeType.Dicom },
            { DicomUID.JPEGLosslessNonHierarchical14, MimeType.Dicom },
            { DicomUID.JPEGLossless, MimeType.Dicom },
            { DicomUID.JPEGLSLossless, MimeType.Dicom },
            { DicomUID.JPEGLSLossyNearLossless, MimeType.Dicom },
            { DicomUID.JPEG2000LosslessOnly, MimeType.Dicom },
            { DicomUID.JPEG2000, MimeType.Dicom },
            { DicomUID.JPEG2000Part2MultiComponentLosslessOnly, MimeType.Dicom },
            { DicomUID.JPEG2000Part2MultiComponent, MimeType.Dicom },
            { DicomUID.MPEG2, MimeType.Dicom },
            { DicomUID.MPEG2MainProfileHighLevel, MimeType.Dicom },
            { DicomUID.MPEG4AVCH264HighProfileLevel41, MimeType.Dicom },
            { DicomUID.MPEG4AVCH264BDCompatibleHighProfileLevel41, MimeType.Dicom },
            { DicomUID.MPEG4AVCH264HighProfileLevel42For2DVideo, MimeType.Dicom },
            { DicomUID.MPEG4AVCH264HighProfileLevel42For3DVideo, MimeType.Dicom },
            { DicomUID.MPEG4AVCH264StereoHighProfileLevel42, MimeType.Dicom },
            { DicomUID.HEVCH265MainProfileLevel51, MimeType.Dicom },
            { DicomUID.HEVCH265Main10ProfileLevel51, MimeType.Dicom }
        };

        public static readonly MediaTypeWithQualityHeaderValue MediaTypeApplicationDicom = new MediaTypeWithQualityHeaderValue(MimeTypeMappings[MimeType.Dicom]);
        public static readonly MediaTypeWithQualityHeaderValue MediaTypeApplicationDicomJson = new MediaTypeWithQualityHeaderValue(MimeTypeMappings[MimeType.DicomJson]);

        public static bool IsValidMediaType(DicomTransferSyntax transferSyntax)
        {
            return SupportedMediaTypes.ContainsKey(transferSyntax.UID);
        }
    }
}