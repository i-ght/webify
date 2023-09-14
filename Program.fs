open System
open System.Threading.Tasks
open System.IO
open CsvHelper
open System.Globalization
open System.Collections.Generic
open System.Text
open System.Xml.Linq

[<AutoOpen>]
module Convenience =
    let split (c: char) (s: string) = s.Split(c)
    
    let trim (s: string) = s.Trim()
    
    let castTask (t: Task<'a>) = (t :> Task)
    

    let streamReader (path: string) =
        new StreamReader(
            path,
            detectEncodingFromByteOrderMarks=true
        )
    let csvReader (reader: TextReader) =
        new CsvReader(
            reader,
            CultureInfo.InvariantCulture
        )
    let csvRecords<'a> (reader: CsvReader) =
        reader.GetRecords<'a>()

    let addArray<'a> (value: 'a) (array: ResizeArray<'a>) = array.Add(value)

    let htmlFilename (date: DateTimeOffset) =
        match date.Month with
        | 1 -> "jan"
        | 2 -> "feb"
        | 3 -> "mar"
        | 4 -> "apr"
        | 5 -> "may"
        | 6 -> "jun"
        | 7 -> "jul"
        | 8 -> "aug"
        | 9 -> "sep"
        | 10 -> "oct"
        | 11 -> "nov"
        | 12 -> "dec"
        | _ -> invalidArg "date" "out of range"
        |> sprintf "%d%s.html" date.Year

    let monthYear (date: DateTimeOffset) = sprintf "%d %s" date.Year (date.ToString("MMMM"))

[<Struct; RequireQualifiedAccess>]
type RecordEntryType =
    | Text

[<Struct; RequireQualifiedAccess>]
type Entry =
    | Text of text: string

type PrimitiveRecord =
    { Date: DateTimeOffset
      Name: string
      DataType: string
      Tags: string
      Data: string }

type Record =
    { Date: DateTimeOffset
      Name: string 
      Tags: string []
      Entry: Entry }

module Html =

    let navCode (records: Record list) =
        let set = HashSet<string>()
        let sb = StringBuilder(8192)

        sb.Append("<div class=\"elevator\">")
        |> ignore<StringBuilder>

        for record in records do
            
            let htmlFileName = htmlFilename record.Date
            
            if set.Add(htmlFileName) then
                let html = [
                    """<div class="floor">"""
                    """<div class="room">"""
                    sprintf """<a class="blocklink" href="%s">""" htmlFileName
                    monthYear record.Date
                    "</a>"
                    "</div>"
                    "</div>"
                ]

                for element in html do
                    sb.AppendLine(element) |> ignore<StringBuilder>

        sb.Append("</div>")
        |> ignore<StringBuilder>

        XElement.Parse(sb.ToString()).ToString()

    let ofRecord (record: Record) =

        let date = record.Date.ToString("yyyy-MM-dd")
        let data = match record.Entry with | Entry.Text text -> text
        
        let html = StringBuilder(8192)

        html
            .AppendFormat("<h3>{0}</h3>", date)
            .AppendLine()
            .AppendFormat("<h4>{0}</h4>", record.Name)
            .AppendLine()
            .AppendFormat(
                "<p>{0}{1}{0}</p>",
                Environment.NewLine,
                data.Replace(Environment.NewLine, sprintf "<br/>%s" Environment.NewLine)
            )
        |> ignore<StringBuilder>

        let html = html.ToString()
        html


let tags (tags: string) =
    split ',' tags
    |> Array.map trim

let records (primitives: PrimitiveRecord seq) = seq {
    for record in primitives do
        let record =
            { Date=record.Date
              Name=record.Name
              Tags=tags record.Tags
              Entry=Entry.Text record.Data }
        yield record
}

let loadRecords () =
    let pathToRecords = "content/content.csv"
    use reader = streamReader pathToRecords
    use reader = csvReader reader

    csvRecords<PrimitiveRecord> reader
    |> records
    |> List.ofSeq


[<EntryPoint>]
let head _argv =
    let records = loadRecords ()    

    let navHtml = Html.navCode records
    let contentHtml = File.ReadAllText("content/codex")

    let content = Dictionary<string, ResizeArray<string>>()
    
    for record in records do
        let key = htmlFilename record.Date
        if not <| content.ContainsKey(key) then
            content.[key] <- ResizeArray<string>(8)
        
        content.[key].Add(Html.ofRecord record)

    for kvp in content do
        let filename = kvp.Key
        let contents = kvp.Value
        contents.Reverse()

        let lines =ResizeArray<string>()
        for contents in contents do
            addArray contents lines

        let content = String.Join(Environment.NewLine, lines)
        let html = contentHtml.Replace("$content", content)
        let html = html.Replace("$nav", navHtml)

        File.WriteAllText(sprintf "content/%s" filename, html)

    0