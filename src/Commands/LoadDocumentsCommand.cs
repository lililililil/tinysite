﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TinySite.Extensions;
using TinySite.Models;

namespace TinySite.Commands
{
    public class LoadDocumentsCommand
    {
        private static readonly Regex DateFromFileName = new Regex(@"^\s*(?<year>\d{4})-(?<month>\d{1,2})-(?<day>\d{1,2})([Tt@](?<hour>\d{1,2})\.(?<minute>\d{1,2})(\.(?<second>\d{1,2}))?)?[-\s]\s*", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.Singleline);
        private static readonly Regex OrderFromFileName = new Regex(@"^\s*(?<order>\d+)\.[-\s]\s*", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.Singleline);

        public string DocumentsPath { private get; set; }

        public string OutputRootPath { private get; set; }

        public string RootUrl { private get; set; }

        public string ApplicationUrl { private get; set; }

        public Author Author { private get; set; }

        public IEnumerable<string> RenderedExtensions { private get; set; }

        public IEnumerable<DocumentFile> Documents { get; private set; }

        public async Task<IEnumerable<DocumentFile>> ExecuteAsync()
        {
            var loadTasks = this.LoadDocumentsAsync();

            return this.Documents = await Task.WhenAll(loadTasks);
        }

        private IEnumerable<Task<DocumentFile>> LoadDocumentsAsync()
        {
            if (Directory.Exists(this.DocumentsPath))
            {
                foreach (var path in Directory.GetFiles(this.DocumentsPath, "*", SearchOption.AllDirectories))
                {
                    yield return this.LoadDocumentAsync(path, LoadDocumentFlags.DateFromFileName | LoadDocumentFlags.InsertDateIntoPath | LoadDocumentFlags.OrderFromFileName | LoadDocumentFlags.SanitizePath | LoadDocumentFlags.CleanUrls, this.RenderedExtensions);
                }
            }
        }

        private async Task<DocumentFile> LoadDocumentAsync(string file, LoadDocumentFlags flags, IEnumerable<string> knownExtensions)
        {
            // Parse the document and update our document metadata.
            //
            var parser = new ParseDocumentCommand();
            parser.DocumentPath = file;
            parser.SummaryMarker = "\r\n\r\n===";
            await parser.ExecuteAsync();

            var metadataDate = parser.Date;

            var order = 0;

            var relativeDocumentPath = Path.GetFullPath(file).Substring(this.DocumentsPath.Length);

            var outputRelativePath = parser.Metadata.Get<string>("output", relativeDocumentPath);
            parser.Metadata.Remove("output");

            // The rest of this function is about calculating the correct
            // name for the file.
            //
            var fileName = Path.GetFileName(outputRelativePath);

            var outputRelativeFolder = Path.GetDirectoryName(outputRelativePath);

            // See if this file should be processed by any of the
            // rendering engines.
            //
            var extensionsForRendering = new List<string>();

            for (; ; )
            {
                var extension = Path.GetExtension(fileName).TrimStart('.');
                if (knownExtensions.Contains(extension))
                {
                    extensionsForRendering.Add(extension);
                    fileName = Path.GetFileNameWithoutExtension(fileName);
                }
                else
                {
                    break;
                }
            }

            if (LoadDocumentFlags.DateFromFileName == (flags & LoadDocumentFlags.DateFromFileName))
            {
                var match = DateFromFileName.Match(fileName);

                if (match.Success)
                {
                    // If the parser metadata didn't specify the date, use the date from the filename.
                    //
                    if (!metadataDate.HasValue)
                    {
                        var year = Convert.ToInt32(match.Groups[1].Value, 10);
                        var month = Convert.ToInt32(match.Groups[2].Value, 10);
                        var day = Convert.ToInt32(match.Groups[3].Value, 10);
                        var hour = match.Groups[4].Success ? Convert.ToInt32(match.Groups[4].Value, 10) : 0;
                        var minute = match.Groups[5].Success ? Convert.ToInt32(match.Groups[5].Value, 10) : 0;
                        var second = match.Groups[6].Success ? Convert.ToInt32(match.Groups[6].Value, 10) : 0;

                        metadataDate = new DateTime(year, month, day, hour, minute, second);
                    }

                    fileName = fileName.Substring(match.Length);
                }
            }

            if (LoadDocumentFlags.OrderFromFileName == (flags & LoadDocumentFlags.OrderFromFileName))
            {
                var match = OrderFromFileName.Match(fileName);

                if (match.Success)
                {
                    order = Convert.ToInt32(match.Groups[1].Value, 10);

                    fileName = fileName.Substring(match.Length);
                }
            }

            var parentId = String.IsNullOrEmpty(outputRelativeFolder) ? null : SanitizePath(outputRelativeFolder);

            if (LoadDocumentFlags.InsertDateIntoPath == (flags & LoadDocumentFlags.InsertDateIntoPath) && metadataDate.HasValue)
            {
                outputRelativeFolder = Path.Combine(outputRelativeFolder, metadataDate.Value.Year.ToString(), metadataDate.Value.Month.ToString(), metadataDate.Value.Day.ToString());
            }

            if (!parser.Metadata.Contains("title"))
            {
                parser.Metadata.Add("title", Path.GetFileNameWithoutExtension(fileName));
            }

            // Sanitize the filename into a good URL.
            //
            var sanitized = SanitizeEntryId(fileName);

            if (!fileName.Equals(sanitized))
            {
                fileName = sanitized;
            }

            if (LoadDocumentFlags.SanitizePath == (flags & LoadDocumentFlags.SanitizePath))
            {
                outputRelativeFolder = SanitizePath(outputRelativeFolder);
            }

            if (LoadDocumentFlags.CleanUrls == (flags & LoadDocumentFlags.CleanUrls) && !"index.html".Equals(fileName, StringComparison.OrdinalIgnoreCase) && ".html".Equals(Path.GetExtension(fileName), StringComparison.OrdinalIgnoreCase))
            {
                outputRelativeFolder = Path.Combine(outputRelativeFolder, Path.GetFileNameWithoutExtension(fileName)) + "\\";

                fileName = null;
            }

            var id = String.IsNullOrEmpty(fileName) ? outputRelativeFolder : Path.Combine(outputRelativeFolder, Path.GetFileNameWithoutExtension(fileName));

            //var parentId = String.IsNullOrEmpty(id) ? null : Path.GetDirectoryName(id);

            var output = Path.Combine(outputRelativeFolder, fileName ?? "index.html");

            var relativeUrl = this.ApplicationUrl.EnsureEndsWith("/") + Path.Combine(outputRelativeFolder, fileName ?? String.Empty).Replace('\\', '/');

            // Finally create the document.
            //
            var documentFile = new DocumentFile(file, this.DocumentsPath, output, this.OutputRootPath, relativeUrl, this.RootUrl, this.Author);

            if (metadataDate.HasValue)
            {
                documentFile.Date = metadataDate.Value;
            }

            documentFile.Id = parser.Metadata.Get<string>("id", id.Trim('\\'));
            parser.Metadata.Remove("id");

            documentFile.ParentId = parser.Metadata.Get<string>("parent", parentId ?? String.Empty);
            parser.Metadata.Remove("parent");

            documentFile.Draft = (parser.Draft || documentFile.Date > DateTime.Now);

            documentFile.ExtensionsForRendering = extensionsForRendering;

            documentFile.Order = parser.Metadata.Get<int>("order", order);
            parser.Metadata.Remove("order");

            documentFile.Paginate = parser.Metadata.Get<int>("paginate", 0);
            parser.Metadata.Remove("paginate");

            documentFile.SourceContent = parser.Content;

            documentFile.Metadata = parser.Metadata;

            return documentFile;
        }

        private static string SanitizeEntryId(string id)
        {
            if (String.IsNullOrWhiteSpace(id))
            {
                return String.Empty;
            }

            id = Regex.Replace(id, @"[^\w\s_\-\.]+", String.Empty); // first, allow only words, spaces, underscores, dashes and dots.
            id = Regex.Replace(id, @"\.{2,}", String.Empty); // strip out any dots stuck together (no pathing attempts).
            id = Regex.Replace(id, @"\s{2,}", " "); // convert multiple spaces into single space.
            id = id.Trim(' ', '.'); // ensure the string does not start or end with a dot
            return id.Replace(' ', '-').ToLowerInvariant(); // finally, replace all spaces with dashes and lowercase it.
        }

        private static string SanitizePath(string id)
        {
            if (String.IsNullOrWhiteSpace(id))
            {
                return String.Empty;
            }

            id = Regex.Replace(id, @"[^\w\-\\/]+", "-"); // first, allow only words, underscores, dashes, and path separators.
            return id.Trim('-').ToLowerInvariant(); // ensure the string does not start or end with dashes and lowercase it.
        }
    }
}
