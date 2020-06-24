module MimeTypes

open System
open System.Collections.Generic
open System.IO

let mappings = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
mappings.Add(".ico", "image/x-icon")
mappings.Add(".html", "text/html")
mappings.Add(".css", "text/css")
mappings.Add(".js", "application/x-javascript")
mappings.Add(".json", "application/json")

let getMimeType (fileInfo : FileInfo) =
    mappings.Item fileInfo.Extension