(*** hide ***)
open ApkHelpers
open System.IO
open Nessos.UnionArgParser


(*** hide ***)
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

        extractZip(apk, outdir, true)

        0

(**
Others examples follow here.
*)
