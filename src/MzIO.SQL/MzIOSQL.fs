﻿namespace MzIO.MzSQL


open System
open System.Data
open System.IO
open System.Threading.Tasks
open System.Collections.Generic
open System.Data.SQLite
open MzIO.Model
open MzIO.Model.CvParam
open MzIO.Json
open MzIO.Binary
open MzIO.IO
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open System.Runtime.InteropServices


type private MzSQLTransactionScope(tr:SQLiteTransaction) =

    interface IDisposable with
        member this.Dispose() =
            tr.Dispose()

    member this.Dispose() =
        (this :> IDisposable).Dispose()

    interface ITransactionScope with

        member this.Commit() =
            tr.Commit()

        member this.Rollback() =
            tr.Rollback()

    member this.Commit() =
        (this :> ITransactionScope).Commit()


    member this.Rollback() =
        (this :> ITransactionScope).Rollback()
    
/// Contains methods and procedures to create, insert and access MzSQL files.
type MzSQL(path) =

    let mutable disposed = false

    let encoder = new BinaryDataEncoder()

    let sqlitePath = 
        if String.IsNullOrWhiteSpace(path) then
                raise (ArgumentNullException("sqlitePath"))
            else
                path

    do
        if File.Exists(sqlitePath) then 
            ()
        else
            let mutable cn' = new SQLiteConnection(sprintf "Data Source=%s;Version=3" sqlitePath)
            cn'.Open()
            MzSQL.SqlInitSchema(cn')
            cn'.Close()
            cn'.Dispose()



    let mutable tmp = new SQLiteConnection(sprintf "Data Source=%s;Version=3" sqlitePath)
    
    member this.cn = tmp

    member this.Open() = this.cn.Open()
    
    member this.model =
        let tr = this.cn.BeginTransaction()
        let potMdoel = MzSQL.trySelectModel(this.cn)
        match potMdoel with
        | Some model    -> model
        | None          -> 
            let tmp = new MzIOModel(Path.GetFileNameWithoutExtension(sqlitePath))
            this.InsertModel tmp
            tr.Commit()
            tr.Dispose()
            this.cn.Close()
            tmp



    /// Initialization of all prePareFunctions for the current connection.
    member this.InsertModel             = MzSQL.prepareInsertModel(this.cn)

    member this.SelectModel             = MzSQL.prepareSelectModel(this.cn)

    member this.UpdateRunIDOfMzIOModel  = MzSQL.prepareUpdateRunIDOfMzIOModel(this.cn)

    member this.InsertMassSpectrum      = MzSQL.prepareInsertMassSpectrum(this.cn)

    member this.SelectMassSpectrum      = MzSQL.prepareSelectMassSpectrum(this.cn)

    member this.SelectMassSpectra       = MzSQL.prepareSelectMassSpectra(this.cn)

    member this.SelectPeak1DArray       = MzSQL.prepareSelectPeak1DArray(this.cn)

    member this.InsertChromatogram      = MzSQL.prepareInsertChromatogram(this.cn)

    member this.SelectChromatogram      = MzSQL.prepareSelectChromatogram(this.cn)

    member this.SelectChromatograms     = MzSQL.prepareSelectChromatograms(this.cn)

    member this.SelectPeak2DArray       = MzSQL.prepareSelectPeak2DArray(this.cn)

    //member this.Commit() = 
    //    MzSQL.RaiseConnectionState(cn)
    //    MzSQL.RaiseTransactionState(cn)
    //    tr.Commit()


    member this.Close() = this.cn.Close() 

    /// Checks whether SQLiteConnection is open or not and reopens it, when is should be closed.
    //static member RaiseConnectionState(cn:SQLiteConnection) =
    //    if (cn.State=ConnectionState.Open) then 
    //        ()
    //    else
    //        failwith "Connection state is not open"

    ///// Checks whether SQLiteConnection is open or not and reopens it, when is should be closed.
    //static member RaiseTransactionState(cn:SQLiteConnection, tr:byref<SQLiteTransaction>) =
    //    if tr.Connection = null then
    //        tr <- cn.BeginTransaction()
    //    else
    //        ()

    /// Checks whether connection is disposed or not and fails when it is.
    member private this.RaiseDisposed() =

            if disposed then 
                raise (new ObjectDisposedException(this.GetType().Name))
            else 
                ()

    /// Creates the tables in the connected dataBase.
    static member private SqlInitSchema(cn:SQLiteConnection) =
        //MzSQL.RaiseConnectionState(cn)
        use cmd = new SQLiteCommand("CREATE TABLE IF NOT EXISTS Model (Lock INTEGER  NOT NULL PRIMARY KEY DEFAULT(0) CHECK (Lock=0), Content TEXT NOT NULL)", cn)
        cmd.ExecuteNonQuery() |> ignore
        use cmd = new SQLiteCommand("CREATE TABLE IF NOT EXISTS Spectrum (RunID TEXT NOT NULL, SpectrumID TEXT NOT NULL PRIMARY KEY, Description TEXT NOT NULL, PeakArray TEXT NOT NULL, PeakData BINARY NOT NULL)", cn)
        cmd.ExecuteNonQuery() |> ignore
        use cmd = new SQLiteCommand("CREATE TABLE IF NOT EXISTS Chromatogram (RunID TEXT NOT NULL, ChromatogramID TEXT NOT NULL PRIMARY KEY, Description TEXT NOT NULL, PeakArray TEXT NOT NULL, PeakData BINARY NOT NULL)", cn)
        cmd.ExecuteNonQuery() |> ignore

    /// Selects model from DB. It has always the same ID and only one Model should be saved per DB.
    static member private trySelectModel(cn:SQLiteConnection) =
        MzSQL.SqlInitSchema(cn)
        //MzSQL.RaiseConnectionState(cn)
        //MzSQL.RaiseTransactionState(cn)
        let querySelect = "SELECT Content FROM Model WHERE Lock = 0"
        let cmdSelect = new SQLiteCommand(querySelect, cn)
        let selectReader = cmdSelect.ExecuteReader()
        let rec loopSelect (reader:SQLiteDataReader) model =
            match reader.Read() with
            | true  -> loopSelect reader (Some(MzIOJson.deSerializeMzIOModel(reader.GetString(0))))
            | false -> model           
        loopSelect selectReader None

    /// Prepare function to insert MzIOModel-JSONString.
    static member private prepareInsertModel(cn:SQLiteConnection) =
        //MzSQL.RaiseConnectionState(cn)
        //MzSQL.RaiseTransactionState(cn)
        let queryString = 
            "INSERT INTO Model (
                Lock,
                Content)
                VALUES(
                    @lock,
                    @content)"
        let cmd = new SQLiteCommand(queryString, cn)
        cmd.Parameters.Add("@lock"      ,Data.DbType.Int64)     |> ignore
        cmd.Parameters.Add("@content"   ,Data.DbType.String)    |> ignore
        (fun (model:MzIOModel) ->
            cmd.Parameters.["@lock"].Value      <- 0
            cmd.Parameters.["@content"].Value   <- MzIOJson.ToJson(model)
            cmd.ExecuteNonQuery() |> ignore
        ) 

    /// Prepare function to select MzIOModel as a MzIOModel object.
    static member prepareSelectModel(cn:SQLiteConnection) =
        let querySelect = "SELECT Content FROM Model WHERE Lock = 0"
        let cmd = new SQLiteCommand(querySelect, cn)
        fun () ->
        let rec loopSelect (reader:SQLiteDataReader) model =
            match reader.Read() with
            | true  -> loopSelect reader (MzIOJson.deSerializeMzIOModel(reader.GetString(0)))
            | false -> model  
        use reader = cmd.ExecuteReader()
        loopSelect reader (new MzIOModel())

    /// Prepare function to upadte runID in MzIOModel in DB.
    static member private prepareUpdateRunIDOfMzIOModel(cn:SQLiteConnection) =        
        //let jsonModel = MzIOJson.ToJson(model)
        let queryString = "UPDATE Model SET Content = @model WHERE Lock = 0"
        let cmd = new SQLiteCommand(queryString, cn)
        let potModel = MzSQL.trySelectModel(cn)
        let insertModel = MzSQL.prepareInsertModel(cn)
        cmd.Parameters.Add("@model" ,Data.DbType.String)    |> ignore
        fun (runID:string) (model:MzIOModel) ->
            if potModel.IsSome then
                let run = 
                    let tmp =
                        model.Runs.GetProperties false
                        |> Seq.head
                        |> (fun item -> item.Value :?> Run)
                    new Run(runID, tmp.SampleID, tmp.DefaultInstrumentID,tmp.DefaultSpectrumProcessing, tmp.DefaultChromatogramProcessing)
                if model.Runs.TryAdd(run.ID, run) then
                    printfn "MzIOJson.ToJson(model) %s" (MzIOJson.ToJson(model))
                    cmd.Parameters.["@model"].Value <- MzIOJson.ToJson(model)
                    cmd.ExecuteNonQuery() |> ignore
                else 
                    ()
            else
                insertModel model

    ///Prepare function to insert MzQuantMLDocument-record.
    static member private prepareInsertMassSpectrum(cn:SQLiteConnection) =
        //MzSQL.RaiseConnectionState(cn)
        //MzSQL.RaiseTransactionState(cn)
        let queryString = 
            "INSERT INTO Spectrum (
                RunID,
                SpectrumID,
                Description,
                PeakArray,
                PeakData)
                VALUES(
                    @runID,
                    @spectrumID,
                    @description,
                    @peakArray,
                    @peakData)"
        let cmd = new SQLiteCommand(queryString, cn)
        cmd.Parameters.Add("@runID"         ,Data.DbType.String)    |> ignore
        cmd.Parameters.Add("@spectrumID"    ,Data.DbType.String)    |> ignore
        cmd.Parameters.Add("@description"   ,Data.DbType.String)    |> ignore
        cmd.Parameters.Add("@peakArray"     ,Data.DbType.String)    |> ignore
        cmd.Parameters.Add("@peakData"      ,Data.DbType.Binary)    |> ignore
        (fun (encoder:BinaryDataEncoder) (runID:string) (spectrum:MassSpectrum) (peaks:Peak1DArray) ->
            let encodedValues = encoder.Encode(peaks)
            cmd.Parameters.["@runID"].Value         <- runID
            cmd.Parameters.["@spectrumID"].Value    <- spectrum.ID
            cmd.Parameters.["@description"].Value   <- MzIOJson.MassSpectrumToJson(spectrum)
            cmd.Parameters.["@peakArray"].Value     <- MzIOJson.ToJson(peaks)
            cmd.Parameters.["@peakData"].Value      <- encodedValues
            cmd.ExecuteNonQuery() |> ignore
        )        

    /// Prepare function to select element of Description table of MzSQL.
    static member private prepareSelectMassSpectrum(cn:SQLiteConnection) =
        //MzSQL.RaiseConnectionState(cn)
        //MzSQL.RaiseTransactionState(cn)
        let queryString = "SELECT Description FROM Spectrum WHERE SpectrumID = @spectrumID"
        let cmd = new SQLiteCommand(queryString, cn)
        cmd.Parameters.Add("@spectrumID", Data.DbType.String) |> ignore
        let rec loop (reader:SQLiteDataReader) (acc:MassSpectrum option) =
            match reader.Read() with
                    | true  -> loop reader (Some (MzIOJson.deSerializeMassSpectrum(reader.GetString(0))))
                    | false -> acc 
        fun (id:string) ->
        cmd.Parameters.["@spectrumID"].Value <- id            
        use reader = cmd.ExecuteReader()
        match loop reader None with
        | Some spectrum -> spectrum
        | None          -> failwith ("No enum with this SpectrumID found")

    /// Prepare function to select elements of Description table of MzSQL.
    static member private prepareSelectMassSpectra(cn:SQLiteConnection) =
        //MzSQL.RaiseConnectionState(cn)
        //MzSQL.RaiseTransactionState(cn)
        let queryString = "SELECT Description FROM Spectrum WHERE RunID = @runID"
        let cmd = new SQLiteCommand(queryString, cn)
        cmd.Parameters.Add("@runID", Data.DbType.String) |> ignore
        let rec loop (reader:SQLiteDataReader) acc =
            match reader.Read() with
            | true  -> loop reader (MzIOJson.deSerializeMassSpectrum(reader.GetString(0))::acc)
            | false -> acc 
        fun (id:string) ->
        cmd.Parameters.["@runID"].Value <- id            
        use reader = cmd.ExecuteReader()            
        loop reader []
        |> (fun spectra -> if spectra.IsEmpty then failwith ("No enum with this RunID found") else spectra)
        |> (fun spectra -> spectra :> IEnumerable<MassSpectrum>)

    /// Prepare function to select elements of PeakArray and PeakData tables of MzSQL.
    static member private prepareSelectPeak1DArray(cn:SQLiteConnection) =
        //MzSQL.RaiseConnectionState(cn)
        //MzSQL.RaiseTransactionState(cn)
        let decoder = new BinaryDataDecoder()
        let queryString = "SELECT PeakArray, PeakData FROM Spectrum WHERE SpectrumID = @spectrumID"
        let cmd = new SQLiteCommand(queryString, cn)
        cmd.Parameters.Add("@spectrumID", Data.DbType.String) |> ignore
        let rec loop (reader:SQLiteDataReader) peaks =
            match reader.Read() with
            | true  -> loop reader (decoder.Decode(reader.GetStream(1), MzIOJson.FromJson<Peak1DArray>(reader.GetString(0))))
            | false -> peaks 
        fun (id:string) ->
        cmd.Parameters.["@spectrumID"].Value <- id            
        use reader = cmd.ExecuteReader()            
        loop reader (new Peak1DArray())

    /// Prepare function to insert element into Chromatogram table of MzSQL.
    static member private prepareInsertChromatogram(cn:SQLiteConnection) =
        //MzSQL.RaiseConnectionState(cn)
        //MzSQL.RaiseTransactionState(cn)
        let encoder = new BinaryDataEncoder()
        let selectModel = MzSQL.prepareSelectModel(cn)
        let updateRunID = MzSQL.prepareUpdateRunIDOfMzIOModel(cn)
        let queryString = 
            "INSERT INTO Chromatogram (
                RunID,
                ChromatogramID,
                Description,
                PeakArray,
                PeakData)
                VALUES(
                    @runID,
                    @chromatogramID,
                    @description,
                    @peakArray,
                    @peakData)"
        let cmd = new SQLiteCommand(queryString, cn)
        cmd.Parameters.Add("@runID"         ,Data.DbType.String)    |> ignore
        cmd.Parameters.Add("@chromatogramID"  ,Data.DbType.String)  |> ignore
        cmd.Parameters.Add("@description"   ,Data.DbType.String)    |> ignore
        cmd.Parameters.Add("@peakArray"     ,Data.DbType.String)    |> ignore
        cmd.Parameters.Add("@peakData"      ,Data.DbType.Binary)    |> ignore
        (fun (runID:string) (chromatogram:Chromatogram) (peaks:Peak2DArray) ->
            updateRunID runID (selectModel())
            cmd.Parameters.["@runID"].Value         <- runID
            cmd.Parameters.["@chromatogramID"].Value  <- chromatogram.ID
            cmd.Parameters.["@description"].Value   <- MzIOJson.ToJson(chromatogram)
            cmd.Parameters.["@peakArray"].Value     <- MzIOJson.ToJson(peaks)
            cmd.Parameters.["@peakData"].Value      <- encoder.Encode(peaks)
            cmd.ExecuteNonQuery() |> ignore
        )        

    /// Prepare function to select element of Description table of MzSQL.
    static member private prepareSelectChromatogram(cn:SQLiteConnection) =
        //MzSQL.RaiseConnectionState(cn)
        //MzSQL.RaiseTransactionState(cn)
        let queryString = "SELECT Description FROM Chromatogram WHERE RunID = @runID"
        let cmd = new SQLiteCommand(queryString, cn)
        cmd.Parameters.Add("@runID", Data.DbType.String) |> ignore
        let rec loop (reader:SQLiteDataReader) (acc:Chromatogram) =
            match reader.Read() with
            | true  -> loop reader (MzIOJson.FromJson<Chromatogram>(reader.GetString(0)))
            | false -> acc 
        fun (id:string) ->
        cmd.Parameters.["@runID"].Value <- id            
        use reader = cmd.ExecuteReader()            
        loop reader (new Chromatogram())

    /// Prepare function to select elements of Description table of MzSQL.
    static member private prepareSelectChromatograms(cn:SQLiteConnection) =
        //MzSQL.RaiseConnectionState(cn)
        //MzSQL.RaiseTransactionState(cn)
        let queryString = "SELECT Description FROM Chromatogram WHERE ChromatogramID = @chromatogramID"
        let cmd = new SQLiteCommand(queryString, cn)
        cmd.Parameters.Add("@chromatogramID", Data.DbType.String) |> ignore
        let rec loop (reader:SQLiteDataReader) acc =
            match reader.Read() with
            | true  -> loop reader (MzIOJson.FromJson<Chromatogram>(reader.GetString(0))::acc)
            | false -> acc 
        fun (id:string) ->
        cmd.Parameters.["@chromatogramID"].Value <- id            
        use reader = cmd.ExecuteReader()            
        loop reader []
        |> (fun spectra -> if spectra.IsEmpty then failwith ("No enum with this RunID found") else spectra)
        |> (fun item -> item :> IEnumerable<Chromatogram>)

    /// Prepare function to select elements of PeakArray and PeakData tables of MzSQL.
    static member private prepareSelectPeak2DArray(cn:SQLiteConnection) =
        //MzSQL.RaiseConnectionState(cn)
        //MzSQL.RaiseTransactionState(cn)
        let decoder = new BinaryDataDecoder()
        let queryString = "SELECT PeakArray, PeakData FROM Chromatogram WHERE ChromatogramID = @chromatogramID"
        let cmd = new SQLiteCommand(queryString, cn)
        cmd.Parameters.Add("@chromatogramID", Data.DbType.String) |> ignore
        let rec loop (reader:SQLiteDataReader) peaks =
            match reader.Read() with
            | true  -> loop reader (decoder.Decode(reader.GetStream(1), MzIOJson.FromJson<Peak2DArray>(reader.GetString(0))))
            | false -> peaks 
        fun (id:string) ->
        cmd.Parameters.["@chromatogramID"].Value <- id            
        use reader = cmd.ExecuteReader()            
        loop reader (new Peak2DArray())

    interface IMzIODataReader with

        /// Read all mass spectra of one run of MzSQL.
        member this.ReadMassSpectra(runID: string) =
            this.RaiseDisposed()
            this.SelectMassSpectra(runID)

        /// Read mass spectrum of MzSQL.
        member this.ReadMassSpectrum(spectrumID: string) =
            this.RaiseDisposed()
            this.SelectMassSpectrum(spectrumID)

        /// Read peaks of mass spectrum of MzSQL.
        member this.ReadSpectrumPeaks(spectrumID: string) =
            this.RaiseDisposed()
            this.SelectPeak1DArray(spectrumID)

        /// Read mass spectrum of MzSQL asynchronously.
        member this.ReadMassSpectrumAsync(spectrumID:string) =    
            let tmp = this :> IMzIODataReader
            async
                {
                    return tmp.ReadMassSpectrum(spectrumID)
                }
            //Task<MzIO.Model.MassSpectrum>.Run(fun () -> this.ReadMassSpectrum(spectrumID))

        /// Read peaks of mass spectrum of MzSQL asynchronously.
        member this.ReadSpectrumPeaksAsync(spectrumID:string) =  
            let tmp = this :> IMzIODataReader
            async
                {
                    return tmp.ReadSpectrumPeaks(spectrumID)
                }
            //Task<Peak1DArray>.Run(fun () -> this.ReadSpectrumPeaks(spectrumID))

        /// Read all chromatograms of one run of MzSQL.
        member this.ReadChromatograms(runID: string) =
            this.RaiseDisposed()
            this.SelectChromatograms(runID)

        /// Read chromatogram of MzSQL.
        member this.ReadChromatogram(chromatogramID: string) =
            this.RaiseDisposed()
            this.SelectChromatogram(chromatogramID)

        /// Read peaks of chromatogram of MzSQL.
        member this.ReadChromatogramPeaks(chromatogramID: string) =
            this.RaiseDisposed()
            this.SelectPeak2DArray(chromatogramID)

        /// Read chromatogram of MzSQL asynchronously.
        member this.ReadChromatogramAsync(chromatogramID:string) =
           async {return this.SelectChromatogram(chromatogramID)}
        
        /// Read peaks of chromatogram of MzSQL asynchronously.
        member this.ReadChromatogramPeaksAsync(chromatogramID:string) =
           async {return this.SelectPeak2DArray(chromatogramID)}

    /// Read all mass spectra of one run of MzSQL.
    member this.ReadMassSpectra(runID: string) =
            (this :> IMzIODataReader).ReadMassSpectra(runID)

    /// Read mass spectrum of MzSQL.
    member this.ReadMassSpectrum(spectrumID: string) =
        (this :> IMzIODataReader).ReadMassSpectrum(spectrumID)

    /// Read peaks of mass spectrum of MzSQL.
    member this.ReadSpectrumPeaks(spectrumID: string) =
        (this :> IMzIODataReader).ReadSpectrumPeaks(spectrumID)

    /// Read mass spectrum of MzSQL asynchronously.
    member this.ReadMassSpectrumAsync(spectrumID:string) =        
        (this :> IMzIODataReader).ReadMassSpectrumAsync(spectrumID)

    /// Read peaks of mass spectrum of MzSQL asynchronously.
    member this.ReadSpectrumPeaksAsync(spectrumID:string) =            
        (this :> IMzIODataReader).ReadSpectrumPeaksAsync(spectrumID)

    /// Read all chromatograms of one run of MzSQL.
    member this.ReadChromatograms(runID: string) =
        (this :> IMzIODataReader).ReadChromatograms(runID)

    /// Read chromatogram of MzSQL.
    member this.ReadChromatogram(chromatogramID: string) =
        (this :> IMzIODataReader).ReadChromatogram(chromatogramID)

    /// Read peaks of chromatogram of MzSQL.
    member this.ReadChromatogramPeaks(chromatogramID: string) =
        (this :> IMzIODataReader).ReadChromatogramPeaks(chromatogramID)

    /// Read chromatogram of MzSQL asynchronously.
    member this.ReadChromatogramAsync(chromatogramID:string) =
        (this :> IMzIODataReader).ReadChromatogramAsync(chromatogramID)
        
    /// Read peaks of chromatogram of MzSQL asynchronously.
    member this.ReadChromatogramPeaksAsync(chromatogramID:string) =
        (this :> IMzIODataReader).ReadChromatogramPeaksAsync(chromatogramID)

    interface IMzIODataWriter with

        member this.InsertMass(runID: string, spectrum: MassSpectrum, peaks: Peak1DArray) =
            this.RaiseDisposed()
            this.InsertMassSpectrum encoder runID spectrum peaks

        member this.InsertChrom(runID: string, chromatogram: Chromatogram, peaks: Peak2DArray) =
            this.RaiseDisposed()
            this.InsertChromatogram runID chromatogram peaks

        member this.InsertAsyncMass(runID: string, spectrum: MassSpectrum, peaks: Peak1DArray) =
            async {return (this.Insert(runID, spectrum, peaks))}

        member this.InsertAsyncChrom(runID: string, chromatogram: Chromatogram, peaks: Peak2DArray) =
            async {return (this.Insert(runID, chromatogram, peaks))}

    /// Write runID, spectrum and peaks into MzSQL file.
    member this.Insert(runID: string, spectrum: MassSpectrum, peaks: Peak1DArray) =
        (this :> IMzIODataWriter).InsertMass(runID, spectrum, peaks)

    /// Write runID, chromatogram and peaks into MzSQL file.
    member this.Insert(runID: string, chromatogram: Chromatogram, peaks: Peak2DArray) =
        (this :> IMzIODataWriter).InsertChrom(runID, chromatogram, peaks)

    /// Write runID, spectrum and peaks into MzSQL file asynchronously.
    member this.InsertAsync(runID: string, spectrum: MassSpectrum, peaks: Peak1DArray) =
        (this :> IMzIODataWriter).InsertAsyncMass(runID, spectrum, peaks)

    /// Write runID, chromatogram and peaks into MzSQL file asynchronously.
    member this.InsertAsync(runID: string, chromatogram: Chromatogram, peaks: Peak2DArray) =
        (this :> IMzIODataWriter).InsertAsyncChrom(runID, chromatogram, peaks)
        
    interface IDisposable with

        /// Disposes everything and closes connection.
        member this.Dispose() =
            disposed <- true            
            this.cn.Close()
            //tr.Dispose()

    /// Disposes everything and closes connection.
    member this.Dispose() =

        (this :> IDisposable).Dispose()


    interface IMzIOIO with

        /// Open connection to MzSQL data base.
        member this.BeginTransaction() =
            if (this.cn.State=ConnectionState.Open) then () else this.cn.Open()
            //MzSQL.RaiseTransactionState(cn)
            let tr = this.cn.BeginTransaction()
            new MzSQLTransactionScope(tr) :> ITransactionScope

        /// Creates MzIOModel based on global metadata in MzSQL or default model when no model was in the db.
        member this.CreateDefaultModel() =
            if (this.cn.State=ConnectionState.Open) then () else this.cn.Open()
            new MzIOModel(Path.GetFileNameWithoutExtension(sqlitePath))

        /// Saves in memory MzIOModel into the MzSQL data base.
        member this.SaveModel() =
            if (this.cn.State=ConnectionState.Open) then () else this.cn.Open()
            this.InsertModel this.Model
            //tr.Commit()

        /// Access MzIOModel in memory.
        member this.Model =
            if (this.cn.State=ConnectionState.Open) then () else this.cn.Open()
            this.model

    /// Open connection to MzSQL data base.
    member this.BeginTransaction() =
        if (this.cn.State=ConnectionState.Open) then () else this.cn.Open()
        this.cn.BeginTransaction()
        //(this :> IMzIOIO).BeginTransaction()

    /// Creates model based on model in MzSQL or default model when no model was in the db.
    member this.CreateDefaultModel() =
        (this :> IMzIOIO).CreateDefaultModel()        

    /// Saves in memory MzIOModel into the MzSQL data base.
    member this.SaveModel() =
        (this :> IMzIOIO).SaveModel()
        
    /// Access MzIOModel in memory.
    member this.Model = 
        (this :> IMzIOIO).Model

    /// Inserts runID, MassSpectra with corresponding Peak1DArrasy into datbase Spectrum table with chosen compression type for the peak data.
    member this.insertMSSpectrum (runID:string) (reader:IMzIODataReader) (compress: BinaryDataCompressionType) (spectrum: MassSpectrum) = 
        let peakArray = reader.ReadSpectrumPeaks(spectrum.ID)
        let clonedP = new Peak1DArray(BinaryDataCompressionType.NoCompression, peakArray.IntensityDataType,peakArray.MzDataType)
        clonedP.Peaks <- peakArray.Peaks
        this.Insert(runID, spectrum, clonedP)

    /// Modifies spectrum according to the used spectrumPeaksModifier and inserts the result into the MzSQL data base. 
    member this.insertModifiedSpectrumBy (spectrumPeaksModifierF: IMzIODataReader -> MassSpectrum -> BinaryDataCompressionType -> Peak1DArray) (runID:string) (reader:IMzIODataReader) (compress: BinaryDataCompressionType) (spectrum: MassSpectrum) = 
        let modifiedP = spectrumPeaksModifierF reader spectrum compress
        this.Insert(runID, spectrum, modifiedP)

    /// Updates the an MzIOModel by adding all values of the other MzIOModel.
    static member internal updateModel(oldModel:MzIOModel, newModel:MzIOModel) =
        oldModel.GetProperties false
        |> Seq.iter (fun item -> newModel.TryAdd(item.Key, item.Value) |> ignore)
        oldModel.Instruments.GetProperties false
        |> Seq.iter (fun item -> newModel.Instruments.TryAdd(item.Key, item.Value) |> ignore)
        oldModel.Runs.GetProperties false
        |> Seq.iter (fun item -> newModel.Runs.TryAdd(item.Key, item.Value) |> ignore)
        oldModel.DataProcessings.GetProperties false
        |> Seq.iter (fun item -> newModel.DataProcessings.TryAdd(item.Key, item.Value) |> ignore)
        oldModel.Softwares.GetProperties false
        |> Seq.iter (fun item -> newModel.Softwares.TryAdd(item.Key, item.Value) |> ignore)
        oldModel.Samples.GetProperties false
        |> Seq.iter (fun item -> newModel.Samples.TryAdd(item.Key, item.Value) |> ignore)
        newModel.FileDescription <- oldModel.FileDescription
        newModel

    /// Starts bulkinsert of mass spectra into a MzLiteSQL database
    member this.insertMSSpectraBy insertSpectrumF (runID:string) (reader:IMzIODataReader) (compress: BinaryDataCompressionType) (spectra: seq<MassSpectrum>) = 
        let selectModel = MzSQL.prepareSelectModel(this.cn)
        let updateRunID = MzSQL.prepareUpdateRunIDOfMzIOModel(this.cn)
        let model = MzSQL.updateModel(selectModel(), reader.Model)
        updateRunID runID model
        let bulkInsert spectra = 
            spectra
            |> Seq.iter (insertSpectrumF runID reader compress)
        bulkInsert spectra
        //this.Commit()
        //this.Dispose()
        //this.Close()

