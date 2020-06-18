module FileScraper
open System.IO

let websitePath = "C:\\Users\\Leon\\source\\repos\\WebServer\\src\\WebServer\\website\\"

let getResourcePath relativePath =
    websitePath + relativePath

let getFileContents (absoluteFilePath : string) =
    use streamReader = new StreamReader(absoluteFilePath)
    streamReader.ReadToEnd()