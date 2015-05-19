﻿
open System.IO
open System.Text
open ICSharpCode.SharpZipLib.Zip
open Nessos.UnionArgParser

let (+/+) (path1:string) (path2:string) = 
    Path.Combine(path1.Replace("/", "\\"), path2.Replace("/", "\\"))

let ensureDirectory path =
    if path |> Directory.Exists |> not then
        path |> Directory.CreateDirectory |> ignore

let emptyDirectory path =
    let d = new DirectoryInfo(path)
    d.GetFiles() |> Seq.iter(fun f -> f.Delete())
    d.GetDirectories () |> Seq.iter(fun f -> f.Delete(true))

let manifestDecode (data: byte array) = 
    // inpired by http://stackoverflow.com/a/19063830/2554459
    let endDocTag = 0x00100101
    let startTag = 0x00100102
    let endTag = 0x00100103

    let lew (arr:byte array, off:int) = 
        let p1 = uint32 (arr.[off + 3]) <<< 24 &&& 0xff000000u
        let p2 = uint32 (arr.[off + 2]) <<< 16 &&& 0xff0000u
        let p3 = uint32 (arr.[off + 1]) <<< 8 &&& 0xff00u
        let p4 = uint32 (arr.[off]) &&& 0xFFu
        p1 ||| p2 ||| p3 ||| p4 |> int32

    let firstTagOffset = 
        let rec scanStartOfFirstTag offset = 
            match lew(data, offset) with
                | v when v = startTag -> Some (offset)
                | _ when offset < data.Length - 4 -> scanStartOfFirstTag (offset + 4)
                | _ -> None
        let s = lew(data, 3 * 4)
        match s |> scanStartOfFirstTag with
            | Some v -> v
            | None -> s

    let readStringAt offset =   
        let len = data.[offset + 1] <<< 8 &&& byte(0xff00) ||| data.[offset] &&& byte(0xff) |> int
        [|for i in 0..len-1 -> data.[offset + 2 + i * 2] |] |> System.Text.Encoding.UTF8.GetString

    let readString (sitOff:int, stOff:int, strInd:int) =
        if strInd < 0 then null else readStringAt (stOff + lew(data, sitOff + strInd * 4))

    let numbStrings = lew(data, 4 * 4)
    let sitOff = 0x24 // Offset of start of StringIndexTable
    let stOff = sitOff + numbStrings * 4 // StringTable follows StrIndexTable
    let xmlTagOff = firstTagOffset // Start from the offset in the 3rd word.
    let rec readNode acc off =
        let tag0 = lew(data, off)
        let lineNo = lew(data, off + 2 * 4)
        let nameSi = lew(data, off + 5 * 4)
            
        match tag0 with
            | t when t = startTag -> 
                let numbAttrs = lew(data, off + 7 * 4)
                let name = readString(sitOff, stOff, nameSi)
                let mutable attrOff = off + 9 * 4
                let sb = new StringBuilder(acc + "<" + name)
                for i in 0..numbAttrs-1 do
                    let attrNameNsSi = lew(data, attrOff)
                    let attrNameSi = lew(data, attrOff + 1 * 4)
                    let attrValueSi = lew(data, attrOff + 2 * 4)
                    let attrFlags = lew(data, attrOff + 3 * 4)
                    let attrResId = lew(data, attrOff + 4 * 4)
                    let attrName = readString(sitOff, stOff, attrNameSi)
                    let attrValue = if not(attrValueSi = -1) then readString (sitOff, stOff, attrValueSi) else attrResId.ToString()
                    (" " + attrName + "=\"" + attrValue + "\"") |> sb.Append |> ignore
                    attrOff <- attrOff + 5 * 4
                sb.Append(">") |> ignore
                readNode (sb.ToString()) attrOff
            | t when t = endTag -> 
                let name = readString(sitOff, stOff, nameSi)
                readNode (acc + "</" + name + ">\r\n") (off + 6 * 4)
            | t when t = endDocTag -> acc
            | _ -> failwith "Invalid manifest format";

    readNode "" xmlTagOff

let humanReadableSize (size:int64) =
    match size with
        | s when s <= 1024L -> sprintf "%d bytes" s
        | s when s <= 1024L * 1024L -> sprintf "%d KB" (s / 1024L)
        //| s when s <= 1024L * 1024L -> sprintf "%d MB" (s / 1024L / 1024L)
        | s -> sprintf "%d MB" (s / 1024L / 1024L)

let extractZip (zipfile, outdir:string) =
    let rec extractEntry (zip:ZipInputStream, current:ZipEntry) =
        printfn "extracting %s (%s)" current.Name (zip.Length |> humanReadableSize)

        if current = null |> not then
            match current.Name  with
                | p when p = "AndroidManifest.xml" -> 
                    use memory = new MemoryStream()
                    zip.CopyTo(memory)
                    zip.Flush()
                    memory.Flush()
                    let manifest = memory.ToArray() |> manifestDecode
                    File.WriteAllText(outdir +/+ p, manifest)
                | entry -> 
                    let p = outdir +/+ entry
                    p |> Path.GetDirectoryName |> ensureDirectory
                    use f = File.Create(p)
                    zip.CopyTo(f)
                    zip.Flush()
                    f.Flush()
                    f.Close()

            extractEntry(zip, zip.GetNextEntry())

    use fs = File.OpenRead(zipfile)
    let s = new ZipInputStream(fs)
    extractEntry(s, s.GetNextEntry()) |> ignore
    

let getZipEntry (filepath, path) =
    let clean (name:string) = if not(name.StartsWith("/")) then ("/" + name) else name
    let rec searchEntry (zip:ZipInputStream, current:ZipEntry) =
        match current.Name |> clean  with
            | name when name = path -> 
                use memory = new MemoryStream()
                zip.CopyTo(memory)
                zip.Flush()
                memory.Flush()
                Some(memory.ToArray())
            | _ -> 
                match zip.GetNextEntry() with 
                    | null -> None
                    | next -> searchEntry(zip, next)

    use fs = File.OpenRead(filepath)
    let s = new ZipInputStream(fs)
    searchEntry(s, s.GetNextEntry())

let getManifest (filepath) =
    match getZipEntry (filepath, "/AndroidManifest.xml") with
        | Some bytes -> Some (bytes |> manifestDecode |> System.Xml.Linq.XDocument.Parse)
        | None -> None


type CLIArguments =
    | [<Mandatory>] Apk of string
    | [<Mandatory>] Outdir of string
with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Apk _ -> "specify a apk."
            | Outdir _ -> "specify an output directory."

[<EntryPoint>]
let main argv = 
    printfn "%A" argv

    let parser = UnionArgParser.Create<CLIArguments>()
    let usage = parser.Usage()
    if argv.Length <= 0 then 
        printfn "%s" usage
        -1
    else
        let results = parser.Parse argv
        let apk = results.GetResult <@ Apk @>
        let outdir = results.GetResult <@ Outdir @>

        outdir |> ensureDirectory
        outdir |> emptyDirectory

        if apk |> File.Exists |> not then
            failwithf "Cannot fond apk %s" apk

        extractZip(apk, outdir)

        0

