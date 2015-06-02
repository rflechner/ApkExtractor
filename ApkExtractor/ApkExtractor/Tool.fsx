(*** hide ***)
#r @"../packages/UnionArgParser/lib/net40/UnionArgParser.dll"
#r @"../packages/SharpZipLib/lib/20/ICSharpCode.SharpZipLib.dll"
#r @"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5.1\System.Xml.Linq.dll"

#I @"../packages/FSharp.Formatting.2.9.6/lib/net40"
#I @"../packages/FSharpVSPowerTools.Core.1.8.0/lib/net45"
#I @"../packages/FSharp.Compiler.Service.0.0.87/lib/net45"

#r @"../packages/FSharp.Formatting.2.9.6/lib/net40/FSharp.Literate.dll"
#r @"../packages/FSharp.Formatting.2.9.6/lib/net40/FSharp.Markdown.dll"
#r @"../packages/FSharp.Formatting.2.9.6/lib/net40/FSharp.CodeFormat.dll"

(*** hide ***)
open System.Xml
open System.Xml.Linq
open System.IO
open System.Text
open ICSharpCode.SharpZipLib.Zip
open Nessos.UnionArgParser

#load "ApkHelpers.fs"

open ApkHelpers


(**
Tools in fsx files
------------------

This function scan a folder and displays the version code of each apk read in the compiled AndroidManifest
*)

let displayApksVersions path = 
    for apk in Directory.GetFiles(path, "*.apk") do
        printfn "- apk: %s" apk
        try
            match getManifest (apk) with
            | Some manifest -> let vc = manifest.Element("manifest" |> XName.Get).Attributes() |> Seq.filter(fun a -> a.Name.LocalName = "versionCode") |> Seq.exactlyOne
                               printfn "- versionCode: %s" vc.Value
            | None -> failwith "Cannot find manifest"
        with e -> printfn "Error: %s" e.Message

(**
This function scan a folder and extracts each apk
*)
let extractAllApks path = 
    for apk in Directory.GetFiles(path, "*.apk") do
        printfn "- apk: %s" apk
        try
            match getManifest (apk) with
            | Some manifest -> 
                let vc = manifest.Element("manifest" |> XName.Get).Attributes() |> Seq.filter(fun a -> a.Name.LocalName = "versionCode") |> Seq.exactlyOne
                printfn "- versionCode: %s" vc.Value
                let output = (Path.Combine(path, apk |> Path.GetFileNameWithoutExtension)) + "-" + vc.Value
                printfn "- output: %s" output
                if output |> Directory.Exists |> not then
                    output |> Directory.CreateDirectory |> ignore
                extractZip(apk, output, false)
            | None -> printfn "Cannot find manifest"
            
        with e -> printfn "Error: %s" e.Message


(*** hide ***)
let path = @"E:\Mobility\YSMobileApplications\pack"
displayApksVersions path
extractAllApks path

(*** hide ***)

open FSharp.Literate
open FSharp.Markdown

let createDocs () =
    Literate.ProcessDirectory(__SOURCE_DIRECTORY__, format=OutputKind.Html, references=false, includeSource=false, assemblyReferences=[], generateAnchors = true)

(*** define:later-bit ***)
    
//    let content = """
//    let a = 10
//    (*** include-value:a ***)"""
//
//    // Create evaluator and parse script
//    let fsi = FsiEvaluator()
//    //Literate.ProcessDirectory(__SOURCE_DIRECTORY__, outputDirectory = Path.Combine(__SOURCE_DIRECTORY__, "../help"))
//
//
//    for sourceFile in Directory.GetFiles(__SOURCE_DIRECTORY__, "*.fs") do
//        let outputDirectory = Path.Combine(__SOURCE_DIRECTORY__, "../help")
//        let fn = Path.GetFileNameWithoutExtension(sourceFile)
//        let htmlfile = Path.Combine(outputDirectory, fn + ".html")
//        //Literate.ProcessScriptFile(sourceFile, output = htmlfile, references = false)
//        //let doc = Literate.ParseScriptString(content, fsiEvaluator = fsi)
//        let doc = Literate.ParseScriptFile(sourceFile, references = false)
//        let html = Literate.WriteHtml(doc)
//        File.WriteAllText(htmlfile, html)
//
//
