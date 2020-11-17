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

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Net;
using System.Threading.Tasks;

namespace Nvidia.Clara.DicomAdapter.Server.Services.Http
{
    /// <summary>
    /// The StateEnumConstraint is used in routing contraint to help convert enum string used in routes to State enum.
    /// </summary>
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILoggerFactory loggerFactory)
        {
            _next = next;
            _logger = loggerFactory.CreateLogger<ExceptionHandlingMiddleware>();
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                var dictionary = new System.Collections.Generic.Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { "CorrelationId", Guid.NewGuid().ToString("D") },
                };
                using (_logger.BeginScope(dictionary))
                {
                    await _next(context);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("HTTP error in request {0}: {1}", context.Request.Path, ex);
                await HandleExceptionAsync(context, ex);
            }
        }

        private Task HandleExceptionAsync(HttpContext context, Exception ex)
        {
            var code = HttpStatusCode.InternalServerError; // 500 if unexpected

            if (ex is ArgumentException) code = HttpStatusCode.BadRequest;

            var result = JsonConvert.SerializeObject(new { error = ex.Message.Replace("\n", " ") });
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)code;

            return context.Response.WriteAsync(result);
        }
    }
}