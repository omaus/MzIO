﻿namespace MzIO.Model


open System.Collections.Generic
open MzIO.Model
open Newtonsoft.Json


/// Expansible description of a processing software.
[<JsonObject(MemberSerialization.OptIn, IsReference = true)>]
type Software [<JsonConstructor>] (id:string) =
    
    inherit ModelItem(id)

    new() = Software("id")

/// The model item container for processing software.
[<Sealed>]
type SoftwareList [<JsonConstructor>] internal (dict:Dictionary<string, obj>) =

    inherit MzIO.Model.ObservableModelItemCollection<Software>(dict)

    new() = new SoftwareList(new Dictionary<string, obj>())

