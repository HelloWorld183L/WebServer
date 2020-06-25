module HttpHandler

open FileScraper
open MimeTypes
open NetworkingTypes
open System
open System.Text

let crlf = "\r\n"

let setUpHttpHeaders contentType =
    "HTTP/1.1 200 OK" + crlf +
    "Server: CustomWebServer 1.0" + crlf +
    "Content-Type: " + contentType + "; charset=utf-8" + crlf +
    "Accept-Ranges: none"
    
let parseHttpRequest (httpRequest : string) =
    let requestParts = httpRequest.Split(' ', StringSplitOptions.RemoveEmptyEntries)
    let httpRequestInfo = { RequestMethod = requestParts.[0]; ResourceName = requestParts.[1] }
    httpRequestInfo

let getHttpHeaders resourceName =
    let httpHeaders =
        getFileInfo resourceName
        |> getMimeType
        |> setUpHttpHeaders
    httpHeaders + crlf + crlf

let addExtraSpace (byteArray : byte[]) =
    let extraSpace =
        crlf + crlf
        |> Encoding.UTF8.GetBytes
    let arrayWithSpace = Array.zeroCreate (byteArray.Length + extraSpace.Length) : byte[]
    byteArray.CopyTo(arrayWithSpace, 0)
    extraSpace.CopyTo(arrayWithSpace, byteArray.Length - 1)
    arrayWithSpace

let assembleSendBuffer resourceName =
    let contentByteData =
        resourceName
        |> getResourcePath
        |> getFileContents
        |> addExtraSpace

    let headerByteData =
        resourceName
        |> getHttpHeaders
        |> Encoding.UTF8.GetBytes
        |> addExtraSpace

    let combinedLength = (contentByteData.Length + headerByteData.Length)
    let byteData = Array.zeroCreate combinedLength : byte[]
    headerByteData.CopyTo(byteData, 0)
    contentByteData.CopyTo(byteData, headerByteData.Length - 1)
    byteData