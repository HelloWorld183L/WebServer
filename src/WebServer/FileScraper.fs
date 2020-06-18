module FileScraper
open System.IO

let basePath = "C:\Users\david\source\repos\WebServer\src\WebServer\website"

let getResourcePath resourceLocation =
    Path.GetFullPath(resourceLocation, basePath)

let getFileContents (absoluteFilePath : string) =
    use streamReader = new StreamReader(absoluteFilePath)
    streamReader.ReadToEnd()
    