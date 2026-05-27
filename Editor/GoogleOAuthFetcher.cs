#if UNITY_EDITOR
using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using UnityEngine;

namespace NorskaLib.Spreadsheets
{
    public static class GoogleOAuthFetcher
    {
        const string CredentialPath = "Assets/Editor/GoogleCredentials/client_secret.json";

        // Reuse the same service across imports in the same Editor session
        static SheetsService _service;

        public static async Task<SheetsService> GetServiceAsync()
        {
            if (_service != null) return _service;

            using var stream = new System.IO.FileStream(
                CredentialPath,
                System.IO.FileMode.Open,
                System.IO.FileAccess.Read);

            var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                GoogleClientSecrets.FromStream(stream).Secrets,
                new[] { SheetsService.Scope.SpreadsheetsReadonly },
                "user",
                CancellationToken.None);

            _service = new SheetsService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "NorskaLibSpreadsheets"
            });

            return _service;
        }

        /// <summary>
        /// Pass this to SpreadsheetImporter as the fetcher delegate.
        /// Extracts the sheet name from the NorskaLib URL format and
        /// calls the Sheets API v4 directly instead of the CSV export endpoint.
        /// </summary>
        public static async Task<string> FetchAsync(string url)
        {
            // NorskaLib URL format:
            // https://docs.google.com/spreadsheets/d/{docId}/gviz/tq?tqx=out:csv&sheet={sheetName}
            var uri = new Uri(url);
            var segments = uri.AbsolutePath.Split('/');

            // docId is the segment after /d/
            var docIdIndex = Array.IndexOf(segments, "d") + 1;
            var docId = segments[docIdIndex];

            // sheet name is in the query string
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var sheetName = query["sheet"];

            var service = await GetServiceAsync();
            var request = service.Spreadsheets.Values.Get(docId, $"{sheetName}!A:ZZ");
            var response = await request.ExecuteAsync();
            var rows = response.Values;

            if (rows == null || rows.Count == 0) return "";

            // Re-serialize to CSV so the existing NorskaLib parser is untouched
            int colCount = rows[0].Count;
            var sb = new System.Text.StringBuilder();
            foreach (var row in rows)
            {
                for (int i = 0; i < colCount; i++)
                {
                    var cell = i < row.Count ? row[i].ToString() : "";
                    // Quote cells that contain commas or newlines
                    if (cell.Contains(',') || cell.Contains('\n') || cell.Contains('"'))
                        cell = $"\"{cell.Replace("\"", "\"\"")}\"";
                    if (i > 0) sb.Append(',');
                    sb.Append(cell);
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}
#endif