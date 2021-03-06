﻿namespace MzIO.Model


open System
open System.Linq.Expressions
open MzIO.Model
open Newtonsoft.Json


///Not implemented fully yet.
[<Sealed>]
[<JsonObject(MemberSerialization = MemberSerialization.OptIn)>]
type RunReference [<JsonConstructor>] (sourceFile:SourceFile, runID:string) =

    [<JsonProperty("SourceFile")>]
    let mutable sourceFile' = sourceFile

    [<JsonProperty("RunID")>]
    let mutable runID' =
        if String.IsNullOrWhiteSpace(runID) then
            raise (ArgumentNullException("runID"))
        else runID

    new() = RunReference(new SourceFile(), "rundID")

    [<JsonProperty(Required = Required.Always)>]
    member this.SourceFile  = sourceFile'

    [<JsonProperty(Required = Required.Always)>]
    member this.RunID       = runID'

    override this.Equals(obj:Object) =
        if Expression.ReferenceEquals(this, obj)=true then true
        else 
            let other = 
                match obj with
                | :? RunReference as obj -> obj
                | _                      -> failwith "Wrong type"
            this.RunID.Equals(other.RunID) && this.SourceFile.Equals(other.SourceFile)
    
    override this.GetHashCode() =
        (this.RunID, this.SourceFile.GetHashCode()).GetHashCode()
