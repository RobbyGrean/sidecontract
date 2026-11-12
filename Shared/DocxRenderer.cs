using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace Sidecontract
{
    internal static class SidecontractDocxRenderer
    {
        private const string WordNs = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        private const string XmlNs = "http://www.w3.org/XML/1998/namespace";
        private static readonly Regex TagRegex = new Regex(@"\{[^{}\r\n]{1,80}\}", RegexOptions.Compiled);

        public static string Render(string templatePath, string requestedOutputPath, IDictionary<string, string> values)
        {
            if (!File.Exists(templatePath)) throw new FileNotFoundException("ไม่พบ Template", templatePath);
            var outputPath = EnsureUniqueOutputPath(requestedOutputPath);
            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory)) Directory.CreateDirectory(outputDirectory);

            try
            {
                using (var source = ZipFile.OpenRead(templatePath))
                using (var target = ZipFile.Open(outputPath, ZipArchiveMode.Create))
                {
                    foreach (var entry in source.Entries)
                    {
                        var newEntry = target.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                        byte[] bytes;
                        using (var input = entry.Open())
                        using (var memory = new MemoryStream())
                        {
                            input.CopyTo(memory);
                            bytes = memory.ToArray();
                        }

                        if (IsWordTextPart(entry.FullName)) bytes = ReplaceTagsInXml(bytes, values);
                        using (var output = newEntry.Open()) output.Write(bytes, 0, bytes.Length);
                    }
                }

                var remaining = FindBusinessTags(outputPath);
                if (remaining.Count > 0)
                {
                    throw new InvalidDataException("ยังมี tag ในเอกสาร " + Path.GetFileName(outputPath) + ": " + string.Join(", ", remaining));
                }
                return outputPath;
            }
            catch
            {
                try
                {
                    if (File.Exists(outputPath)) File.Delete(outputPath);
                }
                catch
                {
                }
                throw;
            }
        }

        public static List<string> FindBusinessTags(string docxPath)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            using (var archive = ZipFile.OpenRead(docxPath))
            {
                foreach (var entry in archive.Entries)
                {
                    if (!IsWordTextPart(entry.FullName)) continue;
                    var xml = new XmlDocument { PreserveWhitespace = true };
                    using (var reader = new StreamReader(entry.Open(), Encoding.UTF8)) xml.Load(reader);
                    var manager = new XmlNamespaceManager(xml.NameTable);
                    manager.AddNamespace("w", WordNs);
                    foreach (XmlNode paragraph in xml.SelectNodes("//w:p[not(.//w:p)]", manager))
                    {
                        var text = string.Join("", paragraph.SelectNodes(".//w:t", manager).Cast<XmlNode>().Select(x => x.InnerText ?? "").ToArray());
                        foreach (Match match in TagRegex.Matches(text)) result.Add(match.Value);
                    }
                }
            }
            return result.OrderBy(x => x, StringComparer.Ordinal).ToList();
        }

        private static bool IsWordTextPart(string name)
        {
            return Regex.IsMatch(name, @"^word/(document|header\d+|footer\d+)\.xml$", RegexOptions.CultureInvariant);
        }

        private static byte[] ReplaceTagsInXml(byte[] bytes, IDictionary<string, string> values)
        {
            var xml = new XmlDocument { PreserveWhitespace = true };
            xml.LoadXml(Encoding.UTF8.GetString(bytes));
            var manager = new XmlNamespaceManager(xml.NameTable);
            manager.AddNamespace("w", WordNs);

            foreach (XmlNode paragraph in xml.SelectNodes("//w:p[not(.//w:p)]", manager))
            {
                ReplaceParagraphTags(paragraph, manager, values, xml);
            }

            using (var memory = new MemoryStream())
            {
                var settings = new XmlWriterSettings { Encoding = new UTF8Encoding(false), OmitXmlDeclaration = false };
                using (var writer = XmlWriter.Create(memory, settings)) xml.Save(writer);
                return memory.ToArray();
            }
        }

        private static void ReplaceParagraphTags(XmlNode paragraph, XmlNamespaceManager manager, IDictionary<string, string> values, XmlDocument document)
        {
            var replacements = new Dictionary<string, string>(StringComparer.Ordinal);
            var replacementIndex = 0;
            while (true)
            {
                var nodes = paragraph.SelectNodes(".//w:t", manager).Cast<XmlNode>().ToList();
                var text = string.Join("", nodes.Select(x => x.InnerText ?? "").ToArray());
                var matches = TagRegex.Matches(text).Cast<Match>().Where(x => values.ContainsKey(x.Value)).ToArray();
                if (matches.Length == 0) break;

                var match = matches[matches.Length - 1];
                var spans = new List<TextSpan>();
                var cursor = 0;
                foreach (var node in nodes)
                {
                    var value = node.InnerText ?? "";
                    spans.Add(new TextSpan { Start = cursor, End = cursor + value.Length, Node = node });
                    cursor += value.Length;
                }

                var touched = spans.Where(x => x.Start < match.Index + match.Length && x.End > match.Index).ToArray();
                if (touched.Length == 0) return;
                var first = touched[0];
                var last = touched[touched.Length - 1];
                var firstText = first.Node.InnerText ?? "";
                var lastText = last.Node.InnerText ?? "";
                var prefixLength = Math.Max(0, match.Index - first.Start);
                var prefix = prefixLength <= firstText.Length ? firstText.Substring(0, prefixLength) : "";
                var suffixStart = Math.Max(0, match.Index + match.Length - last.Start);
                var suffix = suffixStart < lastText.Length ? lastText.Substring(suffixStart) : "";
                var replacement = values[match.Value] ?? "";
                var token = "\uE000DOCUMENT346" + replacementIndex.ToString() + "\uE001";
                replacementIndex++;
                replacements[token] = replacement;
                first.Node.InnerText = prefix + token + suffix;
                var preserve = document.CreateAttribute("xml", "space", XmlNs);
                preserve.Value = "preserve";
                first.Node.Attributes.SetNamedItem(preserve);
                for (var i = 1; i < touched.Length; i++) touched[i].Node.InnerText = "";
            }
            if (replacements.Count == 0) return;
            foreach (var node in paragraph.SelectNodes(".//w:t", manager).Cast<XmlNode>())
            {
                var text = node.InnerText ?? "";
                foreach (var replacement in replacements) text = text.Replace(replacement.Key, replacement.Value ?? "");
                node.InnerText = text;
            }
        }

        private static string EnsureUniqueOutputPath(string path)
        {
            if (!File.Exists(path)) return path;
            var directory = Path.GetDirectoryName(path);
            var name = Path.GetFileNameWithoutExtension(path);
            var extension = Path.GetExtension(path);
            var index = 2;
            string candidate;
            do
            {
                candidate = Path.Combine(directory, name + "_" + index + extension);
                index++;
            }
            while (File.Exists(candidate));
            return candidate;
        }

        private sealed class TextSpan
        {
            public int Start;
            public int End;
            public XmlNode Node;
        }
    }
}
