module FileScraper
open System.IO
open System

let websitePath = "C:\\Users\\Leon\\source\\repos\\WebServer\\src\\WebServer\\website\\"

let getFileInfo relativePath =
    new FileInfo(websitePath + relativePath)

let getFileContents (absoluteFilePath : string) =
    use streamReader = new StreamReader(absoluteFilePath)
    streamReader.ReadToEnd()

let getResourcePath resourceName =
    if String.IsNullOrEmpty(resourceName) then
        let fileInfo = getFileInfo "\\index.html"
        fileInfo.FullName
    else
        let fileInfo = resourceName.Replace('/','\\')
                       |> getFileInfo
        fileInfo.FullName