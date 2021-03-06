﻿namespace MzIO.Wiff


open System
open System.IO
open System.Linq
open System.Threading.Tasks
open System.Collections.Generic
open System.Text.RegularExpressions
open Clearcore2.Data.AnalystDataProvider
open Clearcore2.Data.DataAccess.SampleData
open MzIO.Json
open MzIO.Binary
open MzIO.IO
open MzIO.Model
open MzIO.Model.CvParam
open MzIO.MetaData.UO.UO
open MzIO.MetaData.PSIMSExtension
open MzIO.Commons.Arrays
open MzIO.Commons.Arrays.MzIOArray
open MzIO.Processing


//put in an extra module for improved performance
//module Regex =

    //let regexID =
    //    new Regex(@"sample=(\d+) experiment=(\d+) scan=(\d+)", RegexOptions.Compiled ||| RegexOptions.ECMAScript)

    //let regexSampleIndex =
    //    new Regex(@"sample=(\d+)", RegexOptions.Compiled ||| RegexOptions.ECMAScript)

    //let mutable sampleIndex     = 0
    //let mutable experimentIndex = 0
    //let mutable scanIndex       = 0

//open Regex

type WiffPeaksArray(wiffSpectrum:Clearcore2.Data.MassSpectrum) =

    //let wiffSpectrum = new Clearcore2.Data.MassSpectrum(

    interface IMzIOArray<Peak1D> with

        member this.Length = wiffSpectrum.NumDataPoints

        //potential error source
        member this.Item
            with get (idx:int) =
                if (idx < 0 || idx >= this.Length) then
                    raise (new IndexOutOfRangeException())
                else
                    new Peak1D(wiffSpectrum.GetYValue(idx), wiffSpectrum.GetXValue(idx))

    member this.Length =

        (this :> IMzIOArray<Peak1D>).Length

    member this.Item(idx:int) =

        (this :> IMzIOArray<Peak1D>).Item(idx)

    static member private Yield(wiffSpectrum:Clearcore2.Data.MassSpectrum) =

        let spectrum = Array.create wiffSpectrum.NumDataPoints (new Peak1D())

        for i=0 to wiffSpectrum.NumDataPoints-1 do
            spectrum.[i] <- new Peak1D(wiffSpectrum.GetYValue(i), wiffSpectrum.GetXValue(i))
        spectrum

    interface IEnumerable<Peak1D> with

        member this.GetEnumerator() =
            WiffPeaksArray.Yield(wiffSpectrum).AsEnumerable<Peak1D>().GetEnumerator()

    interface System.Collections.IEnumerable with

        member this.GetEnumerator() =
            WiffPeaksArray.Yield(wiffSpectrum).GetEnumerator()

    member this.GetEnumerator() =

        (this :> IEnumerable<Peak1D>).GetEnumerator()

    //member this.Peak1D (idx:int) =
    //    if (idx < 0 || idx >= this.Length) then
    //        raise (new IndexOutOfRangeException())
    //    else new Peak1D(wiffSpectrum.GetYValue(idx), wiffSpectrum.GetXValue(idx))

type WiffPeak2DArray(wiffSpectrum:Clearcore2.Data.MassSpectrum, scanIndex:int) =

    //let wiffSpectrum = new Clearcore2.Data.MassSpectrum(

    let rt = wiffSpectrum.Info.Experiment.GetRTFromExperimentScanIndex(scanIndex)

    interface IMzIOArray<Peak2D> with

        member this.Length = wiffSpectrum.NumDataPoints

        //potential error source
        member this.Item
            with get (idx:int) =
                if (idx < 0 || idx >= this.Length) then
                    raise (new IndexOutOfRangeException())
                else
                    new Peak2D(wiffSpectrum.GetYValue(idx), wiffSpectrum.GetXValue(idx), rt)

    member this.Length =

        (this :> IMzIOArray<Peak2D>).Length

    member this.Item(idx:int) =

        (this :> IMzIOArray<Peak2D>).Item(idx)

    static member private Yield(wiffSpectrum:Clearcore2.Data.MassSpectrum, scanIndex) =

        let spectrum = Array.create wiffSpectrum.NumDataPoints (new Peak2D())
        
        let rt = wiffSpectrum.Info.Experiment.GetRTFromExperimentScanIndex(scanIndex)

        for i=0 to wiffSpectrum.NumDataPoints-1 do
            spectrum.[i] <- new Peak2D(wiffSpectrum.GetYValue(i), wiffSpectrum.GetXValue(i), rt)
        spectrum

    interface IEnumerable<Peak2D> with

        member this.GetEnumerator() =
            WiffPeak2DArray.Yield(wiffSpectrum, scanIndex).AsEnumerable<Peak2D>().GetEnumerator()

    interface System.Collections.IEnumerable with
        member this.GetEnumerator() =
            WiffPeak2DArray.Yield(wiffSpectrum, scanIndex).GetEnumerator()

    member this.GetEnumerator() =

        (this :> IEnumerable<Peak2D>).GetEnumerator()

    //member this.Peak1D (idx:int) =
    //    if (idx < 0 || idx >= this.Length) then
    //        raise (new IndexOutOfRangeException())
    //    else new Peak1D(wiffSpectrum.GetYValue(idx), wiffSpectrum.GetXValue(idx))

type TICPeak(intensitySum:float, rt:float) =
    
    member this.IntensitySum    = intensitySum

    member this.RetentionTime   = rt

type TIC(binaryDataCompressionType:BinaryDataCompressionType, intDataType, rtDataType, peaks:IMzIOArray<TICPeak>) =
    
    let mutable peaks   = peaks
    
    member this.BinaryDataCompressionType = binaryDataCompressionType

    member this.IntensityDataType       = intDataType

    member this.RetentionTimeDataType   = rtDataType

    member this.Peaks
        with get() = peaks
        and set(value) = peaks <- value



type WiffTransactionScope() =

    interface IDisposable with

        member this.Dispose() =
            ()

    member this.Dispose() =

        (this :> IDisposable).Dispose()

    interface ITransactionScope with

        /// Does Nothing.
        member this.Commit() =
            ()

        /// Does Nothing.
        member this.Rollback() =
            ()

    /// Does Nothing.
    member this.Commit() =

        (this :> ITransactionScope).Commit()

    /// Does Nothing.
    member this.Rollback() =

        (this :> ITransactionScope).Rollback()

/// Contains methods to access spectrum and peak information of wiff files.
type WiffFileReader(dataProvider:AnalystWiffDataProvider, disposed:Boolean, wiffFilePath:string, licenseFilePath:string) =

    let mutable dataProvider    = dataProvider

    let mutable batch           = AnalystDataProviderFactory.CreateBatch(wiffFilePath, dataProvider)

    let mutable disposed        = disposed

    //regular expression to check for repeated occurrences of words in a string
    //retrieves sample, experiment and scan ID
    let regexID = new Regex(@"sample=(\d+) experiment=(\d+) scan=(\d+)", RegexOptions.Compiled ||| RegexOptions.ECMAScript)
    let regexSampleIndex = new Regex(@"sample=(\d+)", RegexOptions.Compiled ||| RegexOptions.ECMAScript)

    //let wiffFileCheck =
    do
        if not (File.Exists(wiffFilePath)) then
            raise (FileNotFoundException("Wiff file does not exist."))
        if (wiffFilePath.Trim() = "") then
            raise (ArgumentNullException("wiffFilePath"))

    //let licenseFileCheck =
    do
        if (licenseFilePath.Trim() = "") then
            raise (ArgumentNullException("licenseFilePath"))
        else 
            WiffFileReader.ReadWiffLicense(licenseFilePath)

    //let mutable wiffFilePath =
    //    if wiffFilePath<>"wiffFilePath" then
    //        if not (File.Exists(wiffFilePath)) then
    //            raise  (new FileNotFoundException("Wiff file not exists."))
    //        else
    //            match wiffFilePath with
    //            | null  -> failwith (ArgumentNullException("WiffFilePath").ToString())
    //            | ""    -> failwith (ArgumentNullException("WiffFilePath").ToString())
    //            | " "   -> failwith (ArgumentNullException("WiffFilePath").ToString())
    //            |   _   -> wiffFilePath
    //    else wiffFilePath

    //let mutable licenseFilePath =
    //    match licenseFilePath with
    //    | null  -> failwith (ArgumentNullException("LicenseFilePath").ToString())
    //    | ""    -> failwith (ArgumentNullException("LicenseFilePath").ToString())
    //    | " "   -> failwith (ArgumentNullException("LicenseFilePath").ToString())
    //    |   _   -> WiffFileReader.ReadWiffLicense(licenseFilePath)

    new(wiffFilePath:string, ?licenseFilePath:string) =
        let licenseFilePath = 
            defaultArg licenseFilePath (
                let appFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                let wiffFolder = Path.Combine(appFolder, @"IOMIQS\Clearcore2\Licensing")
                Directory.CreateDirectory(wiffFolder) |> ignore
                Path.Combine(wiffFolder, "Clearcore2.license.xml")
            )
        if not (File.Exists licenseFilePath) then
            failwithf "No valid license file found at %s.\n Please provide a license path or put a valid license in %s"
                licenseFilePath
                (Path.GetDirectoryName licenseFilePath)
        new WiffFileReader
                            (
                                new AnalystWiffDataProvider(true), false, wiffFilePath, licenseFilePath
                            )

    //new() = new WiffFileReader(new AnalystWiffDataProvider(), null, false, @"wiffFilePath", @"licenseFilePath", new MzIOModel())

    //static member private regexID =
    //    new Regex(@"sample=(\d+) experiment=(\d+) scan=(\d+)", RegexOptions.Compiled ||| RegexOptions.ECMAScript)

    //static member private regexSampleIndex =
    //    new Regex(@"sample=(\d+)", RegexOptions.Compiled ||| RegexOptions.ECMAScript)

    /// Get sampleIndex based on runID, which is a whole run in a wiff file.
    member private this.ParseByRunID(runID:string, sampleIndex:byref<int>) =

        let match' = regexSampleIndex.Match(runID)
        if match'.Success=true then

            //try
            let groups = match'.Groups
            sampleIndex <- int (groups.[1].Value)

            //with
            //    | :? FormatException ->
            //        raise (new FormatException(sprintf "%s%s" "Error parsing wiff sample index: " runID))
        else
            raise (new FormatException("Not a valid wiff sample index format: " + runID))

    /// Get wiff file specific sampleIndex, sampleIndex and experimentIndex in order to generate runID.
    member private this.ParseBySpectrumID(spectrumID:string, sampleIndex:byref<int>, experimentIndex:byref<int>, scanIndex:byref<int>) =

        let match' = regexID.Match(spectrumID)

        if match'.Success then

            //try
            //This part  causes the slowness of the whole funtion.
            let groups = match'.Groups
            sampleIndex     <- (int (groups.[1].Value))
            experimentIndex <- (int (groups.[2].Value))
            scanIndex       <- (int (groups.[3].Value))

            //with
            //    | :? FormatException -> raise (new FormatException(sprintf "%s%s" "Error parsing wiff spectrum id format: " spectrumID)).ToString())
        else
            raise (new FormatException(sprintf "%s%s" "Not a valid wiff spectrum id format: " spectrumID))

    /// Gets isolationWindow, target M/Z and offset.
    static member GetIsolationWindow(exp:MSExperiment, isoWidth:byref<double>, targetMz:byref<double>) =

        let mr  = exp.Details.MassRangeInfo

        isoWidth <- double 0.

        targetMz <- double 0.

        if mr.Length>0 then

            let mri = mr.[0] :?> FragmentBasedScanMassRange

            isoWidth <- mri.IsolationWindow * (double 0.5)

            targetMz <- double mri.FixedMasses.[0]

            mri <> null
        else
            false

    /// Gets isolationWindow, target M/Z and offset.
    static member GetIsolationWindowProduct(exp:MSExperiment, isoWidth:byref<double>, targetMz:byref<double>) =

        let mr  = exp.Details.MassRangeInfo

        isoWidth <- double 0.

        targetMz <- double 0.

        if mr.Length>1 then

            let mri = mr.[1] :?> FragmentBasedScanMassRange

            isoWidth <- mri.IsolationWindow * (double 0.5)

            targetMz <- double mri.FixedMasses.[0]

            mri <> null
        else
            false

    /// Generate spectrumID based on sampleIndex, experimentIndex and scanIndex.
    static member private ToSpectrumID(sampleIndex:int, experimentIndex:int, scanIndex:int) =

        String.Format("sample={0} experiment={1} scan={2}", sampleIndex, experimentIndex, scanIndex)

    static member private GetSpectrum(batch:Batch, sample:MassSpectrometerSample, msExp:MSExperiment, sampleIndex:int, experimentIndex:int, scanIndex:int) =

        let wiffSpectrum    = msExp.GetMassSpectrumInfo(scanIndex)
        let MzIOSpectrum    = new MassSpectrum(WiffFileReader.ToSpectrumID(sampleIndex, experimentIndex, scanIndex))

        // spectrum

        MzIOSpectrum.SetMsLevel(wiffSpectrum.MSLevel) |> ignore

        if wiffSpectrum.CentroidMode=true then
            MzIOSpectrum.SetCentroidSpectrum()        |> ignore
        else
            MzIOSpectrum.SetProfileSpectrum()         |> ignore

        // scan
        let mutable scan = new Scan()
        scan.SetScanStartTime(wiffSpectrum.StartRT).UO_Minute() |> ignore
        MzIOSpectrum.Scans.Add(Guid.NewGuid().ToString(), scan)

        // precursor
        let precursor = new Precursor()
        let mutable isoWidth = double 0
        let mutable targetMz = double 0

        if wiffSpectrum.IsProductSpectrum then
            if WiffFileReader.GetIsolationWindow(wiffSpectrum.Experiment, & isoWidth, & targetMz)=true
            then
                precursor.IsolationWindow.SetIsolationWindowTargetMz(targetMz)      |> ignore
                precursor.IsolationWindow.SetIsolationWindowUpperOffset(isoWidth)   |> ignore
                precursor.IsolationWindow.SetIsolationWindowLowerOffset(isoWidth)   |> ignore
            let selectedIon = new SelectedIon()
            selectedIon.SetSelectedIonMz(wiffSpectrum.ParentMZ)                     |> ignore
            selectedIon.SetChargeState(wiffSpectrum.ParentChargeState)              |> ignore
            precursor.SelectedIons.Add(Guid.NewGuid().ToString(), selectedIon)
            precursor.Activation.SetCollisionEnergy(wiffSpectrum.CollisionEnergy)   |> ignore
            MzIOSpectrum.Precursors.Add(Guid.NewGuid().ToString(), precursor)
            MzIOSpectrum

        else
            MzIOSpectrum

    static member private GetChrom(batch:Batch, sample:MassSpectrometerSample, msExp:MSExperiment, sampleIndex:int, experimentIndex:int, scanIndex:int) =

        let wiffSpectrum    = msExp.GetMassSpectrumInfo(scanIndex)
        let MzIOChrom       = new Chromatogram(WiffFileReader.ToSpectrumID(sampleIndex, experimentIndex, scanIndex))

        if wiffSpectrum.IsProductSpectrum then
            
            // precursor
            let precursor = new Precursor()
            let mutable isoWidth = double 0
            let mutable targetMz = double 0
            
            if WiffFileReader.GetIsolationWindow(wiffSpectrum.Experiment, & isoWidth, & targetMz)=true
            then
                precursor.IsolationWindow.SetIsolationWindowTargetMz(targetMz)      |> ignore
                precursor.IsolationWindow.SetIsolationWindowUpperOffset(isoWidth)   |> ignore
                precursor.IsolationWindow.SetIsolationWindowLowerOffset(isoWidth)   |> ignore
            let selectedIon = new SelectedIon()
            selectedIon.SetSelectedIonMz(wiffSpectrum.ParentMZ)                     |> ignore
            selectedIon.SetChargeState(wiffSpectrum.ParentChargeState)              |> ignore
            precursor.SelectedIons.Add(Guid.NewGuid().ToString(), selectedIon)
            precursor.Activation.SetCollisionEnergy(wiffSpectrum.CollisionEnergy)   |> ignore
            MzIOChrom.Precursors.Add(Guid.NewGuid().ToString(), precursor)
            

            // product
            let product = new Product()
            let mutable isoWidth_Prod = double 0
            let mutable targetMz_Prod = double 0
            if WiffFileReader.GetIsolationWindowProduct(wiffSpectrum.Experiment, & isoWidth, & targetMz)=true
            then
                product.IsolationWindow.SetIsolationWindowTargetMz(targetMz)      |> ignore
                product.IsolationWindow.SetIsolationWindowUpperOffset(isoWidth)   |> ignore
                product.IsolationWindow.SetIsolationWindowLowerOffset(isoWidth)   |> ignore
                MzIOChrom
            else
                MzIOChrom
        else
            MzIOChrom

    /// Generates runID based on sampleIndex.
    static member private ToRunID(sample: int) =
        String.Format("sample={0}", sample)

    static member Yield(batch:Batch, sampleIndex:int) =

        use sample = batch.GetSample(sampleIndex).MassSpectrometerSample
        (
            let tmp =
                seq{
                    for experimentIndex= 0 to sample.ExperimentCount-1 do
                        let mutable msExp = sample.GetMSExperiment(experimentIndex)
                        for scanIndex = 0 to msExp.Details.NumberOfScans-1 do
                            yield WiffFileReader.GetSpectrum(batch, sample, msExp, sampleIndex, experimentIndex, scanIndex)
                    }
            tmp.AsEnumerable<MassSpectrum>()
        )

        //let mutable massSpectra = []
        //use sample = batch.GetSample(sampleIndex).MassSpectrometerSample
        //(
        //for experimentIndex=0 to sample.ExperimentCount-1 do
        //    use msExp = sample.GetMSExperiment(experimentIndex)
        //    (
        //        for scanIndex=0 to msExp.Details.NumberOfScans-1 do
        //            massSpectra <- WiffFileReader.GetSpectrum(batch, sample, msExp,sampleIndex, experimentIndex, scanIndex) :: massSpectra
        //    )
        //)
        //(List.rev massSpectra).AsEnumerable<PeakList.MassSpectrum>()

    static member YieldChrom(batch:Batch, sampleIndex:int) =

        use sample = batch.GetSample(sampleIndex).MassSpectrometerSample
        (
            let tmp =
                seq{
                    for experimentIndex= 0 to sample.ExperimentCount-1 do
                        let mutable msExp = sample.GetMSExperiment(experimentIndex)
                        for scanIndex = 0 to msExp.Details.NumberOfScans-1 do
                            yield WiffFileReader.GetChrom(batch, sample, msExp, sampleIndex, experimentIndex, scanIndex)
                    }
            tmp.AsEnumerable<Chromatogram>()
        )

    /// Checks whether connection is disposed or not and fails when it is.
    member private this.RaiseDisposed() =

        if disposed = true then 
            raise (new ObjectDisposedException(this.GetType().Name))
        else ()

    interface IDisposable with

        /// Sets disposed to true and disables work with this instance of the ThermoRawFileReader.
        member this.Dispose() =
            if disposed = true then
                ()
            else
                if dataProvider<>null then
                    dataProvider.Close()
                disposed <- true

    /// Sets disposed to true and disables work with this instance of the ThermoRawFileReader.
    member this.Dispose() =

        (this :> IDisposable).Dispose()

    /// In memory MzIOModel of WiffFileReader.
    member private this.model = 
        MzIOJson.HandleExternalModelFile(this, WiffFileReader.GetModelFilePath(wiffFilePath))

    //potentiel failure due to exception
    interface IMzIOIO with
        
        /// Creates connection to wiff file.
        member this.BeginTransaction() =
            this.RaiseDisposed()
            new WiffTransactionScope() :> ITransactionScope

        /// Creates in memory MzIOModel based on ShadowFile or if there is none from wiff file global meta data.
        member this.CreateDefaultModel() =

            this.RaiseDisposed()

            let model = new MzIOModel(batch.Name)

            let sampleNames = batch.GetSampleNames()

            let sourceFile = new SourceFile(batch.Name)
            sourceFile.AddCvParam (new CvParam<IConvertible>("MS:1000770"))
            model.FileDescription.FileContent.AddCvParam(new CvParam<IConvertible>("MS:1000580"))
            model.FileDescription.SourceFiles.Add(sourceFile.ID, sourceFile)

            for sampleIdx=0 to sampleNames.Length-1 do
                use wiffSample = batch.GetSample(sampleIdx)
                (
                    let sampleName = sampleNames.[sampleIdx].Trim()
                    let sampleID = WiffFileReader.ToRunID(sampleIdx)
                    let msSample = wiffSample.MassSpectrometerSample
                    let MzIOSample =
                        new Sample
                            (
                                sampleID,
                                sampleName
                            )
                    model.Samples.Add(MzIOSample.ID, MzIOSample)
                    let softwareID = wiffSample.Details.SoftwareVersion.Trim()
                    let software = new Software(softwareID)
                    let analystSoftware = new CvParam<IConvertible>("MS:1000551")
                    software.AddCvParam(analystSoftware)
                    (
                        if model.Softwares.TryGetItemByKey(softwareID, software)=false then
                            model.Softwares.Add(software.ID, software)
                    )
                    let instrumentID = msSample.InstrumentName.Trim()
                    let instrument = new Instrument(instrumentID, new Software(wiffSample.Details.SoftwareVersion.Trim()))
                    let instrumentName = new UserParam<IConvertible>(wiffSample.MassSpectrometerSample.InstrumentName)
                    instrument.AddUserParam(instrumentName)
                    (
                        if model.Instruments.TryGetItemByKey(instrumentID, instrument)=false then
                            model.Instruments.Add(instrument.ID, instrument)
                    )
                    let runID = String.Format("sample={0}", sampleIdx)
                    let run = new Run(runID, sampleID, instrumentID)
                    model.Runs.Add(run.ID, run)
                    model.FileDescription.Contact.AddCvParam(new CvParam<string>("MS:1000586", ParamValue<string>.CvValue(String.Concat(wiffSample.Details.UserName.TakeWhile(fun item -> item <> '\\')))))
                )
            model

        /// Saves in memory MzIOModel in the shadow file.
        member this.SaveModel() =

            MzIOJson.SaveJsonFile(this.Model, WiffFileReader.GetModelFilePath(wiffFilePath))

        /// Current in memory MzIOModel.
        member this.Model =
            this.RaiseDisposed()
            this.model

    member this.BeginTransaction() =

        (this :> IMzIOIO).BeginTransaction()

    member this.CreateDefaultModel() =

        (this :> IMzIOIO).CreateDefaultModel()

    /// Saves in memory MzIOModel in the shadow file.
    member this.SaveModel() =

        (this :> IMzIOIO).SaveModel()

    /// Access in memory MzIOModel.
    member this.Model =
        
        (this :> IMzIOIO).Model

    //potentiel failure due to exception
    interface IMzIODataReader with

        /// Read mass spectra of wiff file.
        member this.ReadMassSpectra(runID:string) =
            this.RaiseDisposed()
            let mutable sampleIndex = 0
            this.ParseByRunID(runID, & sampleIndex)
            WiffFileReader.Yield(batch, sampleIndex)

        /// Read mass spectrum of wiff file.
        member this.ReadMassSpectrum(spectrumID:string) =
            this.RaiseDisposed()
            let mutable sampleIndex     = 0
            let mutable experimentIndex = 0
            let mutable scanIndex       = 0
            this.ParseBySpectrumID(spectrumID, & sampleIndex, & experimentIndex, & scanIndex)
            use sample      = batch.GetSample(sampleIndex).MassSpectrometerSample
            use msExp       = sample.GetMSExperiment(experimentIndex)
            (WiffFileReader.GetSpectrum(batch, sample, msExp, sampleIndex, experimentIndex, scanIndex))            

        /// Read peaks of spectrum of wiff file.
        member this.ReadSpectrumPeaks(spectrumID:string) =
            this.RaiseDisposed()
            let mutable sampleIndex     = 0
            let mutable experimentIndex = 0
            let mutable scanIndex       = 0            
            this.ParseBySpectrumID(spectrumID, & sampleIndex, & experimentIndex, & scanIndex)
            use sample  = batch.GetSample(sampleIndex).MassSpectrometerSample
            use msExp   = sample.GetMSExperiment(experimentIndex)
            let ms      = msExp.GetMassSpectrum(scanIndex)
            let pa      = new Peak1DArray(BinaryDataCompressionType.NoCompression, BinaryDataType.Float64, BinaryDataType.Float64)

            pa.Peaks <- new WiffPeaksArray(ms)
            pa

        /// Read mass spectrum of wiff file asynchronously.
        member this.ReadMassSpectrumAsync(spectrumID:string) =        
            let tmp = this :> IMzIODataReader
            async
                {
                    return tmp.ReadMassSpectrum(spectrumID)
                }
            //Task<MzIO.Model.MassSpectrum>.Run(fun () -> this.ReadMassSpectrum(spectrumID))

        /// Read peaks of spectra of wiff file asynchronously.
        member this.ReadSpectrumPeaksAsync(spectrumID:string) =            
            let tmp = this :> IMzIODataReader
            async
                {
                    return tmp.ReadSpectrumPeaks(spectrumID)
                }
            //Task<Peak1DArray>.Run(fun () -> this.ReadSpectrumPeaks(spectrumID))

        /// Not implemented yet.
        member this.ReadChromatograms(runID:string) =
            this.RaiseDisposed()
            let mutable sampleIndex = 0
            this.ParseByRunID(runID, & sampleIndex)
            WiffFileReader.YieldChrom(batch, sampleIndex)            

        /// Not implemented yet.
        member this.ReadChromatogram(spectrumID:string) =
            this.RaiseDisposed()
            let mutable sampleIndex     = 0
            let mutable experimentIndex = 0
            let mutable scanIndex       = 0
            this.ParseBySpectrumID(spectrumID, & sampleIndex, & experimentIndex, & scanIndex)
            use sample      = batch.GetSample(sampleIndex).MassSpectrometerSample
            use msExp       = sample.GetMSExperiment(experimentIndex)
            (WiffFileReader.GetChrom(batch, sample, msExp, sampleIndex, experimentIndex, scanIndex))

        /// Not implemented yet.
        member this.ReadChromatogramPeaks(spectrumID:string) =
            this.GetChromatogramPeaks(spectrumID)

        /// Not implemented yet.
        member this.ReadChromatogramAsync(spectrumID:string) =
            let tmp = this :> IMzIODataReader
            async
                {
                    return tmp.ReadChromatogram(spectrumID)
                }

        /// Not implemented yet.
        member this.ReadChromatogramPeaksAsync(spectrumID:string) =
            let tmp = this :> IMzIODataReader
            async
                {
                    return tmp.ReadChromatogramPeaks(spectrumID)
                }

    /// Read all mass spectra of one run of baf file.
    member this.ReadMassSpectra(runID:string)               =
        (this :> IMzIODataReader).ReadMassSpectra(runID)

    /// Read mass spectrum of baf file.
    member this.ReadMassSpectrum(spectrumID:string)         =
        (this :> IMzIODataReader).ReadMassSpectrum(spectrumID)

    /// Read peaks of mass spectrum of baf file.
    member this.ReadSpectrumPeaks(spectrumID:string)        =
        (this :> IMzIODataReader).ReadSpectrumPeaks(spectrumID)

    /// Read mass spectrum of baf file asynchronously.
    member this.ReadMassSpectrumAsync(spectrumID:string)    =
        (this :> IMzIODataReader).ReadMassSpectrumAsync(spectrumID)

    /// Read peaks of mass spectrum of baf file asynchronously.
    member this.ReadSpectrumPeaksAsync(spectrumID:string)   =
        (this :> IMzIODataReader).ReadSpectrumPeaksAsync(spectrumID)

    /// Not implemented yet.
    member this.ReadChromatograms(runID:string)             =
        (this :> IMzIODataReader).ReadChromatograms(runID)

    /// Not implemented yet.
    member this.ReadChromatogramPeaks(runID:string)         =
        (this :> IMzIODataReader).ReadChromatogramPeaks(runID)

    /// Not implemented yet.
    member this.ReadChromatogramAsync(runID:string)         =
        (this :> IMzIODataReader).ReadChromatogramAsync(runID)

    /// Not implemented yet.
    member this.ReadChromatogramPeaksAsync(runID:string)    =
        (this :> IMzIODataReader).ReadChromatogramPeaksAsync(runID)

    //potential error source because text isn't splitted into several keys
    static member ReadWiffLicense(licensePath:string) =
        if not (File.Exists(licensePath)) then
            raise  (new FileNotFoundException("Missing Clearcore2 license file: " + licensePath))
        let text = File.ReadAllText(licensePath)
        Clearcore2.Licensing.LicenseKeys.Keys <- [|text|]

    /// Generaes path for shadow file based on wiffFilePath.
    static member GetModelFilePath(wiffFilePath) =

        sprintf "%s%s" wiffFilePath ".MzIOmodel"

    /// Gets sample, experiment and scanIndex based on spectrumID.
    member private this.getSampleIndex(spectrumID:string) =

        let match' = regexID.Match(spectrumID)

        if match'.Success then
            let groups = match'.Groups
            (int (groups.[1].Value))
        else
            raise (new FormatException(sprintf "%s%s" "Not a valid wiff spectrum id format: " spectrumID))

    /// Returns scan time of mass range.
    static member private getScanTime(massRange:MassRange) =
        match massRange with
        | :? FullScanMassRange  -> (massRange :?> FullScanMassRange).ScanTime
        | _     ->  failwith "No supported type for casting"

    /// Returns scan time of spectrum.
    member this.GetScanTime(spectrumID:string) =
        let sampleIndex = this.getSampleIndex(spectrumID)
        use sample = batch.GetSample(sampleIndex).MassSpectrometerSample
        seq
            {
                for experimentIndex= 0 to sample.ExperimentCount-1 do
                    let mutable msExp = sample.GetMSExperiment(experimentIndex)
                    let scanTimes =
                        msExp.Details.MassRangeInfo
                        |> Array.fold (fun start scanTime -> start + WiffFileReader.getScanTime scanTime) 0.
                    yield scanTimes
            }
        |> Seq.sum

        
    /// Returns TIC of spectrum.
    member this.GetTICOfRun(runID:string) =
        let mutable sampleIndex = 0
        this.ParseByRunID(runID, &sampleIndex)
        let sample = batch.GetSample(sampleIndex).MassSpectrometerSample
        sample.GetTotalIonChromatogram().NumDataPoints

    /// Returns TIC of spectrum.
    member this.GetTICOfSpectrum(spectrumID:string) =
        let mutable sampleIndex     = 0
        let mutable experimentIndex = 0
        let mutable scanIndex       = 0            
        this.ParseBySpectrumID(spectrumID, & sampleIndex, & experimentIndex, & scanIndex)
        let sample  = batch.GetSample(sampleIndex).MassSpectrometerSample
        let msExp   = sample.GetMSExperiment(experimentIndex)
        let ms      = msExp.GetMassSpectrum(scanIndex)
        //msExp.GetTotalIonChromatogram().GetActualXValues().Length
        ms.NumDataPoints

    ///// Returns TIC of whole run.
    //member this.GetTotalTIC(runID:string) =
    //    this.ReadMassSpectra(runID)
    //    |> Seq.fold (fun start spectrum -> start + this.GetTIC(spectrum.ID)) 0

    /// Returns dwell time of spectrum.
    member this.GetDwellTime(spectrumID:string) =
        let sampleIndex = this.getSampleIndex(spectrumID)
        use sample = batch.GetSample(sampleIndex).MassSpectrometerSample
        seq
            {
                for experimentIndex= 0 to sample.ExperimentCount-1 do
                    let mutable msExp = sample.GetMSExperiment(experimentIndex)
                    let dewllTimes =
                        msExp.Details.MassRangeInfo
                        |> Seq.map (fun info -> info.DwellTime)
                    yield dewllTimes
            }
    
    /// Returns dilution factor of spectrum.
    member this.GetDilutionFactor(spectrumID:string) =
        let sampleIndex = this.getSampleIndex(spectrumID)
        use sample = batch.GetSample(sampleIndex).MassSpectrometerSample
        seq
            {
                let mutable msExp = sample.Sample.Details
                yield msExp.DilutionFactor
            }

    /// Returns mass range of spectrum.
    member this.GetMassRange(spectrumID:string) =
        this.RaiseDisposed()
        let mutable sampleIndex     = 0
        let mutable experimentIndex = 0
        let mutable scanIndex       = 0            
        this.ParseBySpectrumID(spectrumID, & sampleIndex, & experimentIndex, & scanIndex)
        use sample  = batch.GetSample(sampleIndex).MassSpectrometerSample
        sample.GetMSExperiment(experimentIndex).Details.StartMass, sample.GetMSExperiment(experimentIndex).Details.EndMass

    /// Returns isolation window of spectrum.
    member this.GetIsolationWindow(spectrumID:string) =
        let sampleIndex = this.getSampleIndex(spectrumID)
        use sample = batch.GetSample(sampleIndex).MassSpectrometerSample
        seq
            {
                for experimentIndex= 0 to sample.ExperimentCount-1 do
                    let mutable msExp = sample.GetMSExperiment(experimentIndex)
                    yield msExp.Details.MassRangeInfo
            }
        |> Seq.distinctBy(fun item -> item.[0].Name)

    /// Sample PeriodIndex
    member this.GetSamplePeriodIndex(spectrumID:string) =
        let sampleIndex = this.getSampleIndex(spectrumID)
        let sample = batch.GetSample(sampleIndex).MassSpectrometerSample
        seq
            {
                for experimentIndex= 0 to sample.ExperimentCount-1 do
                    let mutable msExp = sample.GetMSExperiment(experimentIndex)
                    for scanIndex = 0 to msExp.Details.NumberOfScans do
                        yield msExp.GetMassSpectrum(scanIndex).Info.PeriodIndex
            }
        //|> Seq.head

    /// Sample TransitionIndex
    member this.GetSampleTransitionIndex(spectrumID:string) =
        let sampleIndex = this.getSampleIndex(spectrumID)
        let sample = batch.GetSample(sampleIndex).MassSpectrometerSample
        seq
            {
                for experimentIndex= 0 to sample.ExperimentCount-1 do
                    let mutable msExp = sample.GetMSExperiment(experimentIndex)
                    for scanIndex = 0 to msExp.Details.NumberOfScans do
                        yield msExp.GetMassSpectrum(scanIndex).Info.TransitionIndex
            }
        //|> Seq.head


    /// Sample Info

    /// Sample
    member this.GetSample(spectrumID:string) =
        let sampleIndex = this.getSampleIndex(spectrumID)
        let sample = batch.GetSample(sampleIndex).MassSpectrometerSample
        sample.Sample.SampleLocator.ContainerPath

    /// SampleName
    member this.GetSampleName(spectrumID:string) =
        let sampleIndex = this.getSampleIndex(spectrumID)
        let sample = batch.GetSample(sampleIndex).MassSpectrometerSample
        sample.Sample.Details.SampleName

     /// SampleID
    member this.GetSampleID(spectrumID:string) =
        let sampleIndex = this.getSampleIndex(spectrumID)
        let sample = batch.GetSample(sampleIndex).MassSpectrometerSample
        sample.Sample.Details.SampleID

    /// SampleComment
    member this.GetSampleComment(spectrumID:string) =
        let sampleIndex = this.getSampleIndex(spectrumID)
        let sample = batch.GetSample(sampleIndex).MassSpectrometerSample
        sample.Sample.Details.SampleComment

    ///Acquisition Info

    /// AcquisitionDate
    member this.GetAcquisitionDate(spectrumID) =
        let sampleIndex = this.getSampleIndex(spectrumID)
        let sample = batch.GetSample(sampleIndex).MassSpectrometerSample
        sample.Sample.Details.AcquisitionDateTime.Date

    member private this.getTime(spectrumID) =
        let sampleIndex = this.getSampleIndex(spectrumID)
        let sample = batch.GetSample(sampleIndex).MassSpectrometerSample
        sample.Sample.Details.AcquisitionDateTime

    /// AcquisitionTime
    member this.GetAcquisitionTime(spectrumID) =
        let sampleIndex = this.getSampleIndex(spectrumID)
        let sample = batch.GetSample(sampleIndex).MassSpectrometerSample
        sample.Sample.Details.AcquisitionDateTime.TimeOfDay
        
    /// UserName
    member this.GetUserName(spectrumID:string) =
        let sampleIndex = this.getSampleIndex(spectrumID)
        let sample = batch.GetSample(sampleIndex).MassSpectrometerSample
        sample.Sample.Details.UserName

    /// AcquisitionMethod
    member this.GetAcqusitionMethod(spectrumID:string) =
        let sampleIndex = this.getSampleIndex(spectrumID)
        let sample = batch.GetSample(sampleIndex).MassSpectrometerSample
        sample.Sample.Details.AcquisitionMethodName

    /// Rack
    member this.GetRack(spectrumID:string) =
        let sampleIndex = this.getSampleIndex(spectrumID)
        let sample = batch.GetSample(sampleIndex).MassSpectrometerSample
        sample.Sample.Details.Rack

    /// Plate
    member this.GetPlate(spectrumID:string) =
        let sampleIndex = this.getSampleIndex(spectrumID)
        let sample = batch.GetSample(sampleIndex).MassSpectrometerSample
        sample.Sample.Details.Plate

    /// Vial
    member this.GetVial(spectrumID:string) =
        let sampleIndex = this.getSampleIndex(spectrumID)
        let sample = batch.GetSample(sampleIndex).MassSpectrometerSample
        sample.Sample.Details.Vial

    ///Log

    ///// Eksigent AS3 v4.2
    //member this.GetEksigent(spectrumID:string) =
    //    let sampleIndex = this.getSampleIndex(spectrumID)
    //    let sample = batch.GetSample(sampleIndex).MassSpectrometerSample
    //    for experimentIndex= 0 to sample.ExperimentCount-1 do
    //        let mutable msExp = sample.GetMSExperiment(experimentIndex)
    //        msExp.Details

    /// InstrumentSerialNumber
    member this.GetInstrumentSerialNumber(spectrumID:string) =
        let sampleIndex = this.getSampleIndex(spectrumID)
        let sample = batch.GetSample(sampleIndex).MassSpectrometerSample
        sample.Sample.Details.InstrumentSerialNumber

    /// Sample RowName
    member this.GetSampleRowName(spectrumID:string) =
        let sampleIndex = this.getSampleIndex(spectrumID)
        let sample = batch.GetSample(sampleIndex).MassSpectrometerSample
        seq
            {
                for experimentIndex= 0 to sample.ExperimentCount-1 do
                    let mutable msExp = sample.GetMSExperiment(experimentIndex)
                    for scanIndex = 0 to msExp.Details.NumberOfScans do
                        yield msExp.GetMassSpectrum(scanIndex).Info.XName
            }
        |> Seq.head

    /// Sample RowUnits
    member this.GetSampleRowUnits(spectrumID:string) =
        let sampleIndex = this.getSampleIndex(spectrumID)
        let sample = batch.GetSample(sampleIndex).MassSpectrometerSample
        seq
            {
                for experimentIndex= 0 to sample.ExperimentCount-1 do
                    let mutable msExp = sample.GetMSExperiment(experimentIndex)
                    for scanIndex = 0 to msExp.Details.NumberOfScans do
                        yield msExp.GetMassSpectrum(scanIndex).Info.XUnits
            }
        |> Seq.head

    /// Sample ColumnName
    member this.GetSampleColumnName(spectrumID:string) =
        let sampleIndex = this.getSampleIndex(spectrumID)
        let sample = batch.GetSample(sampleIndex).MassSpectrometerSample
        seq
            {
                for experimentIndex= 0 to sample.ExperimentCount-1 do
                    let mutable msExp = sample.GetMSExperiment(experimentIndex)
                    for scanIndex = 0 to msExp.Details.NumberOfScans do
                        yield msExp.GetMassSpectrum(scanIndex).Info.YName
            }
        |> Seq.head

    /// Sample ColumnUnits
    member this.GetSampleColumnUnits(spectrumID:string) =
        let sampleIndex = this.getSampleIndex(spectrumID)
        let sample = batch.GetSample(sampleIndex).MassSpectrometerSample
        seq
            {
                for experimentIndex= 0 to sample.ExperimentCount-1 do
                    let mutable msExp = sample.GetMSExperiment(experimentIndex)
                    for scanIndex = 0 to msExp.Details.NumberOfScans do
                        yield msExp.GetMassSpectrum(scanIndex).Info.YUnits
            }
        |> Seq.head

    /// Returns massrange of spectrum.
    member this.GetParameters(spectrumID:string) =
        let sampleIndex = this.getSampleIndex(spectrumID)
        use sample = batch.GetSample(sampleIndex).MassSpectrometerSample
        seq
            {
                for experimentIndex= 0 to sample.ExperimentCount-1 do
                    let mutable msExp = sample.GetMSExperiment(experimentIndex)
                    yield msExp.Details.Parameters
            }
    
    /// RowValues
    member this.GetRowValues(spectrumID:string) =
        let sampleIndex = this.getSampleIndex(spectrumID)
        let sample = batch.GetSample(sampleIndex).MassSpectrometerSample
        seq
            {
                for experimentIndex= 0 to sample.ExperimentCount-1 do
                    let mutable msExp = sample.GetMSExperiment(experimentIndex)
                    for scanIndex = 0 to msExp.Details.NumberOfScans do
                        yield msExp.GetMassSpectrum(scanIndex).GetActualXValues()
            }

    /// ColumnValues
    member this.GetColumnValues(spectrumID:string) =
        let sampleIndex = this.getSampleIndex(spectrumID)
        let sample = batch.GetSample(sampleIndex).MassSpectrometerSample
        seq
            {
                for experimentIndex= 0 to sample.ExperimentCount-1 do
                    let mutable msExp = sample.GetMSExperiment(experimentIndex)
                    for scanIndex = 0 to msExp.Details.NumberOfScans do
                        yield msExp.GetMassSpectrum(scanIndex).GetActualYValues()
            }

    /// SaturationValues
    member this.GetRSaturationValues(spectrumID:string) =
        let sampleIndex = this.getSampleIndex(spectrumID)
        let sample = batch.GetSample(sampleIndex).MassSpectrometerSample
        seq
            {
                for experimentIndex= 0 to sample.ExperimentCount-1 do
                    let mutable msExp = sample.GetMSExperiment(experimentIndex)
                    for scanIndex = 0 to msExp.Details.NumberOfScans do
                        yield msExp.GetMassSpectrum(scanIndex).Info.Experiment.Details.SaturationThreshold
            }

    /// Returns TIC of spectrum.
    member this.GetXValuesOfChromatogram(spectrumID:string) =
        let sampleIndex = this.getSampleIndex(spectrumID)
        let msSample = batch.GetSample(sampleIndex).MassSpectrometerSample
        msSample.GetTotalIonChromatogram().GetActualXValues()

    /// Returns TIC of spectrum.
    member this.GetXUnitOfChromatogram(spectrumID:string) =
        let sampleIndex = this.getSampleIndex(spectrumID)
        let msSample = batch.GetSample(sampleIndex).MassSpectrometerSample
        msSample.GetTotalIonChromatogram().Info.XName

    /// Returns TIC of spectrum.
    member this.GetYValuesOfChromatogram(spectrumID:string) =
        let sampleIndex = this.getSampleIndex(spectrumID)
        let msSample = batch.GetSample(sampleIndex).MassSpectrometerSample
        msSample.GetTotalIonChromatogram().GetActualYValues()
        
    /// Returns TIC of spectrum.
    member this.GetYUnitOfChromatogram(spectrumID:string) =
        let sampleIndex = this.getSampleIndex(spectrumID)
        let msSample = batch.GetSample(sampleIndex).MassSpectrometerSample
        msSample.GetTotalIonChromatogram().Info.YName

    /// Returns TIC of spectrum.
    member this.GetXValuesOfChromatogram(spectrumID:string, msLevel:int) =
        let sampleIndex = this.getSampleIndex(spectrumID)
        let msSample = batch.GetSample(sampleIndex).MassSpectrometerSample
        
        match msLevel with
        | 1 -> msSample.GetTotalIonChromatogram().GetActualXValues()
        | 2 ->        
            let msExpIndex = [|1..msSample.ExperimentCount-1|]
            msExpIndex
            |> Array.collect (fun expIndex -> 
                let msExp = msSample.GetMSExperiment(expIndex)
                let scanIndex = [|0..msExp.Details.NumberOfScans-1|]
                scanIndex
                |> Array.map (fun scanIdx -> msExp.GetTotalIonChromatogram().GetXValue(scanIdx)))

    /// Returns TIC of spectrum.
    member this.GetYValuesOfChromatogram(spectrumID:string, msLevel:int) =
        let sampleIndex = this.getSampleIndex(spectrumID)
        let msSample = batch.GetSample(sampleIndex).MassSpectrometerSample
        
        match msLevel with
        | 1 -> msSample.GetTotalIonChromatogram().GetActualYValues()
        | 2 ->        
            let msExpIndex = [|1..msSample.ExperimentCount-1|]
            msExpIndex
            |> Array.collect (fun expIndex -> 
                let msExp = msSample.GetMSExperiment(expIndex)
                let scanIndex = [|0..msExp.Details.NumberOfScans-1|]
                scanIndex
                |> Array.map (fun scanIdx -> msExp.GetTotalIonChromatogram().GetYValue(scanIdx)))
        | _ -> failwith "Only MS1 and MS2 exist!"

    /// Returns TIC of spectrum.
    member this.GetChromatograms(spectrumID:string, msLevel:int) =
        let sampleIndex = this.getSampleIndex(spectrumID)
        let msSample = batch.GetSample(sampleIndex).MassSpectrometerSample
        let pa  = new Peak2DArray(BinaryDataCompressionType.NoCompression, BinaryDataType.Float64, BinaryDataType.Float64, BinaryDataType.Float64)
        match msLevel with
        //| 1 -> msSample.GetTotalIonChromatogram().GetActualYValues()
        | 2 ->        
                let msExpIndex = [|1..msSample.ExperimentCount-1|]
                msExpIndex
                |> Array.map (fun expIndex -> 
                    let peaks =
                        let msExp = msSample.GetMSExperiment(expIndex)
                        let scanIndex = [|0..msExp.Details.NumberOfScans-1|]
                        scanIndex
                        |> Array.collect (fun scanIdx ->
                            let massSpectrum = msExp.GetMassSpectrum(scanIdx)                    
                            let msIndex = [|0..massSpectrum.NumDataPoints-1|]
                            msIndex
                            |> Array.map (fun msIdx ->
                                Peak2D(massSpectrum.GetYValue(msIdx), massSpectrum.GetXValue(msIdx), msExp.GetTotalIonChromatogram().GetXValue(scanIdx))))
                    pa.Peaks <- (peaks.ToMzIOArray())
                    pa)
        | _ -> failwith "Only MS1 and MS2 exist!"

    member this.GetChromatogramPeaks(spectrumID) =
        this.RaiseDisposed()
        let mutable sampleIndex     = 0
        let mutable experimentIndex = 0
        let mutable scanIndex       = 0            
        this.ParseBySpectrumID(spectrumID, & sampleIndex, & experimentIndex, & scanIndex)
        use sample  = batch.GetSample(sampleIndex).MassSpectrometerSample
        use msExp   = sample.GetMSExperiment(experimentIndex)
        let ms      = msExp.GetMassSpectrum(scanIndex)
        let pa      = new Peak2DArray(BinaryDataCompressionType.NoCompression, BinaryDataType.Float64, BinaryDataType.Float64, BinaryDataType.Float64)

        pa.Peaks <- new WiffPeak2DArray(ms, scanIndex)
        pa

    static member YieldChrom(batch:Batch, sampleIndex:int, msLevel:int) =

        use sample = batch.GetSample(sampleIndex).MassSpectrometerSample
        (
            let tmp =
                seq{
                    for experimentIndex= 0 to sample.ExperimentCount-1 do
                        let mutable msExp = sample.GetMSExperiment(experimentIndex)
                        for scanIndex = 0 to msExp.Details.NumberOfScans-1 do
                            let mutable ms = msExp.GetMassSpectrum(scanIndex)
                            if ms.Info.MSLevel = msLevel then
                                let mutable pa = new WiffPeak2DArray(ms, scanIndex)
                                yield Some (new Peak2DArray(BinaryDataCompressionType.NoCompression, BinaryDataType.Float64, BinaryDataType.Float64, BinaryDataType.Float64, pa))
                            else
                                yield None
                }
            let tmp2 = Seq.choose(fun item -> item) tmp
            Seq.sortBy (fun (item:Peak2DArray) -> item.Peaks.[0].Rt) tmp2 |> ignore
            tmp2.AsEnumerable<Peak2DArray>()
        )

    static member YieldChromPeaks(batch:Batch, sampleIndex:int) =

        use sample = batch.GetSample(sampleIndex).MassSpectrometerSample
        (
            let tmp =
                seq{
                    for experimentIndex= 0 to sample.ExperimentCount-1 do
                        let mutable msExp = sample.GetMSExperiment(experimentIndex)
                        for scanIndex = 0 to msExp.Details.NumberOfScans-1 do
                            let mutable ms = msExp.GetMassSpectrum(scanIndex)
                            let mutable pa = new WiffPeak2DArray(ms, scanIndex)
                            yield Some (new Peak2DArray(BinaryDataCompressionType.NoCompression, BinaryDataType.Float64, BinaryDataType.Float64, BinaryDataType.Float64, pa))
                    }
            let tmp2 = Seq.choose(fun item -> item) tmp
            Seq.sortBy (fun (item:Peak2DArray) -> item.Peaks.[0].Rt) tmp2 |> ignore
            tmp2.AsEnumerable<Peak2DArray>()
        )

    member this.GetChromatogramsOfMSLevel(runID:string, msLevel:int) =        
        this.RaiseDisposed()
        let mutable sampleIndex = 0
        this.ParseByRunID(runID, & sampleIndex)
        WiffFileReader.YieldChrom(batch, sampleIndex, msLevel)

    member this.GetChromatogramsPeaks(runID:string) =        
        this.RaiseDisposed()
        let mutable sampleIndex = 0
        this.ParseByRunID(runID, & sampleIndex)
        WiffFileReader.YieldChromPeaks(batch, sampleIndex)

    member this.GetExperimentCount(spectrumID:string) =
        let sampleIndex = this.getSampleIndex(spectrumID)
        let msSample = batch.GetSample(sampleIndex).MassSpectrometerSample
        msSample.ExperimentCount

    member this.GetTotalTICOfSpectrum(spectrumID) =
        this.RaiseDisposed()
        let mutable sampleIndex     = 0
        let mutable experimentIndex = 0
        let mutable scanIndex       = 0            
        this.ParseBySpectrumID(spectrumID, & sampleIndex, & experimentIndex, & scanIndex)
        use sample  = batch.GetSample(sampleIndex).MassSpectrometerSample
        use msExp   = sample.GetMSExperiment(experimentIndex)
        let ticRT   = msExp.GetTotalIonChromatogram().GetXValue(scanIndex)
        let ticInt  = msExp.GetTotalIonChromatogram().GetYValue(scanIndex)        
        let ticPeak = new TICPeak(ticInt, ticRT)
        ticPeak

    member this.GetTIC(runID) =
        this.RaiseDisposed()
        let mutable sampleIndex = 0          
        this.ParseByRunID(runID, & sampleIndex)
        let sample      = batch.GetSample(sampleIndex).MassSpectrometerSample
        let TICRTs      = sample.GetTotalIonChromatogram().GetActualXValues()
        let TICInts     = sample.GetTotalIonChromatogram().GetActualYValues()
        let ticPeaks    = 
            Array.map2 (fun intSum rt -> new TICPeak(intSum, rt)) TICInts TICRTs
            |> Array.sortBy (fun item -> item.RetentionTime)
        new TIC(BinaryDataCompressionType.NoCompression, BinaryDataType.Float64, BinaryDataType.Float64, ticPeaks.ToMzIOArray())

    static member YieldTIC(batch:Batch, sampleIndex:int, msLevel:int) =

        use sample = batch.GetSample(sampleIndex).MassSpectrometerSample
        (
            let pa =
                seq{
                    for experimentIndex= 0 to sample.ExperimentCount-1 do
                        let mutable msExp = sample.GetMSExperiment(experimentIndex)
                        for scanIndex = 0 to msExp.Details.NumberOfScans-1 do
                            let mutable ms = msExp.GetMassSpectrum(scanIndex)
                            if ms.Info.MSLevel = msLevel then
                                let mutable intSum = ms.GetActualYValues().Sum()
                                let mutable rt     = msExp.GetTotalIonChromatogram().GetXValue(scanIndex)                                
                                yield Some (new TICPeak(intSum, rt))
                            else
                                yield None
                    }
            let tmp = Array.choose(fun item -> item) (pa.ToArray())
            let tic = new TIC(BinaryDataCompressionType.NoCompression, BinaryDataType.Float64, BinaryDataType.Float64, tmp.ToMzIOArray())
            Seq.sortBy (fun (item:TICPeak) -> item.RetentionTime) tic.Peaks |> ignore
            tic
        )

    member this.GetTICOfMSLevel(runID, msLevel:int) =
        this.RaiseDisposed()
        let mutable sampleIndex = 0          
        this.ParseByRunID(runID, & sampleIndex)
        WiffFileReader.YieldTIC(batch, sampleIndex, msLevel)

    member this.GetSpectrumIDsOfRt(runID:string, rt:float) =
        this.RaiseDisposed()
        let mutable sampleIndex     = 0        
        this.ParseByRunID(runID, & sampleIndex)
        let sample  = batch.GetSample(sampleIndex).MassSpectrometerSample
        let msExperiments = sample.FindExperiments(rt)
        let experimentIndexes =
            msExperiments
            |> Array.map (fun item -> item.GetMassSpectrum(rt).Info.ExperimentIndex)
        let scanIndexes=
            msExperiments
            |> Array.map (fun item -> item.RetentionTimeToExperimentScan(rt))
        Array.map2 (fun msExp scanIndex -> WiffFileReader.ToSpectrumID(sampleIndex, msExp, scanIndex)) experimentIndexes scanIndexes

    member this.GetSpectrumIDsOfRtRange(runID:string, rtQuery:RangeQuery, ?stepSize:float) =
        let stepSize' = defaultArg stepSize 0.01
        let rtRange = [rtQuery.LowValue..stepSize'..rtQuery.HighValue]
        rtRange
        |> Seq.collect (fun rt -> this.GetSpectrumIDsOfRt(runID, rt))

    member this.GetSpectraOfRt(runID:string, rt:float) =
        this.RaiseDisposed()
        let mutable sampleIndex     = 0        
        this.ParseByRunID(runID, & sampleIndex)
        let sample  = batch.GetSample(sampleIndex).MassSpectrometerSample
        let msExperiments = sample.FindExperiments(rt)
        let experimentIndexes =
            msExperiments
            |> Array.map (fun item -> item.GetMassSpectrum(rt).Info.ExperimentIndex)
        let scanIndexes=
            msExperiments
            |> Array.map (fun item -> item.RetentionTimeToExperimentScan(rt))
        Array.map2 (fun msExp scanIndex -> this.ReadMassSpectrum(WiffFileReader.ToSpectrumID(sampleIndex, msExp, scanIndex))) experimentIndexes scanIndexes

    member this.GetSpectraOfRtRange(runID:string, rtQuery:RangeQuery, ?stepSize:float) =
        let stepSize' = defaultArg stepSize 0.01
        let rtRange = [rtQuery.LowValue..stepSize'..rtQuery.HighValue]
        rtRange
        |> Seq.collect (fun rt -> this.GetSpectraOfRt(runID, rt))
            
     //member this.GetSpectrumIDsOfRtWindow(runID:string, startRT:float, endRT:float) =
     //   this.RaiseDisposed()
     //   let mutable sampleIndex     = 0        
     //   this.ParseByRunID(runID, & sampleIndex)
     //   let sample  = batch.GetSample(sampleIndex).MassSpectrometerSample
     //   let msExperiments = sample.GetMSExperiment(0).Details
     //   let experimentIndexes =
     //       msExperiments
     //       |> Array.map (fun item -> item.GetMassSpectrum(rt).Info.ExperimentIndex)
     //   let scanIndexes=
     //       msExperiments
     //       |> Array.map (fun item -> item.RetentionTimeToExperimentScan(rt))
     //   Array.map2 (fun msExp scanIndex -> WiffFileReader.ToSpectrumID(sampleIndex, msExp, scanIndex)) experimentIndexes scanIndexes

    member this.GetSoftwareVersion(runID:string) =
        this.RaiseDisposed()
        let mutable sampleIndex     = 0        
        this.ParseByRunID(runID, & sampleIndex)
        let sample  = batch.GetSample(sampleIndex).MassSpectrometerSample.Sample
        sample.Details.SoftwareVersion

    member this.GetInstrumentName(runID:string) =
        this.RaiseDisposed()
        let mutable sampleIndex     = 0        
        this.ParseByRunID(runID, & sampleIndex)
        let sample  = batch.GetSample(sampleIndex).MassSpectrometerSample.Sample
        sample.MassSpectrometerSample.InstrumentName

    member this.GetWiffFolderLocation() =
        dataProvider.GetWiffFolderLocation()

    member this.GetDataProviderName() =
        dataProvider.RootProject.Name

    member this.GetSampleLocatorPath(runID:string) =
        this.RaiseDisposed()
        let mutable sampleIndex     = 0        
        this.ParseByRunID(runID, & sampleIndex)
        let sample  = batch.GetSample(sampleIndex).MassSpectrometerSample.Sample
        sample.SampleLocator.ContainerPath

    member this.GetDefaultResolution(spectrumID) =
        this.RaiseDisposed()
        let mutable sampleIndex     = 0
        let mutable experimentIndex = 0
        let mutable scanIndex       = 0            
        this.ParseBySpectrumID(spectrumID, & sampleIndex, & experimentIndex, & scanIndex)
        use sample  = batch.GetSample(sampleIndex).MassSpectrometerSample
        sample.GetMSExperiment(experimentIndex).Details.DefaultResolution
        
    member this.GetExperimentType(spectrumID) =
        this.RaiseDisposed()
        let mutable sampleIndex     = 0
        let mutable experimentIndex = 0
        let mutable scanIndex       = 0            
        this.ParseBySpectrumID(spectrumID, & sampleIndex, & experimentIndex, & scanIndex)
        let sample  = batch.GetSample(sampleIndex).MassSpectrometerSample
        let msExp   = sample.GetMSExperiment(experimentIndex)
        msExp.Details.ExperimentType

    member this.GetPolarity(spectrumID) =
        this.RaiseDisposed()
        let mutable sampleIndex     = 0
        let mutable experimentIndex = 0
        let mutable scanIndex       = 0            
        this.ParseBySpectrumID(spectrumID, & sampleIndex, & experimentIndex, & scanIndex)
        let sample  = batch.GetSample(sampleIndex).MassSpectrometerSample
        let msExp   = sample.GetMSExperiment(experimentIndex)
        msExp.Details.Polarity

    member this.GetNumberOfScans(spectrumID) =
        this.RaiseDisposed()
        let mutable sampleIndex     = 0
        let mutable experimentIndex = 0
        let mutable scanIndex       = 0            
        this.ParseBySpectrumID(spectrumID, & sampleIndex, & experimentIndex, & scanIndex)
        let sample  = batch.GetSample(sampleIndex).MassSpectrometerSample
        let msExp   = sample.GetMSExperiment(experimentIndex)
        msExp.Details.NumberOfScans

    member this.GetSourceType(spectrumID) =
        this.RaiseDisposed()
        let mutable sampleIndex     = 0
        let mutable experimentIndex = 0
        let mutable scanIndex       = 0            
        this.ParseBySpectrumID(spectrumID, & sampleIndex, & experimentIndex, & scanIndex)
        let sample  = batch.GetSample(sampleIndex).MassSpectrometerSample
        let msExp   = sample.GetMSExperiment(experimentIndex)
        msExp.Details.SourceType

    member this.GetRawDataType(spectrumID) =
        this.RaiseDisposed()
        let mutable sampleIndex     = 0
        let mutable experimentIndex = 0
        let mutable scanIndex       = 0            
        this.ParseBySpectrumID(spectrumID, & sampleIndex, & experimentIndex, & scanIndex)
        let sample  = batch.GetSample(sampleIndex).MassSpectrometerSample
        let msExp   = sample.GetMSExperiment(experimentIndex)
        msExp.Details.RawDataType

    member this.GetSpectrumType(spectrumID) =
        this.RaiseDisposed()
        let mutable sampleIndex     = 0
        let mutable experimentIndex = 0
        let mutable scanIndex       = 0            
        this.ParseBySpectrumID(spectrumID, & sampleIndex, & experimentIndex, & scanIndex)
        let sample  = batch.GetSample(sampleIndex).MassSpectrometerSample
        let msExp   = sample.GetMSExperiment(experimentIndex)
        msExp.Details.SpectrumType

    member this.GetSaturationThreshold(spectrumID) =
        this.RaiseDisposed()
        let mutable sampleIndex     = 0
        let mutable experimentIndex = 0
        let mutable scanIndex       = 0            
        this.ParseBySpectrumID(spectrumID, & sampleIndex, & experimentIndex, & scanIndex)
        let sample  = batch.GetSample(sampleIndex).MassSpectrometerSample
        let msExp   = sample.GetMSExperiment(experimentIndex)
        msExp.Details.SaturationThreshold

    member this.GetIDAType(spectrumID) =
        this.RaiseDisposed()
        let mutable sampleIndex     = 0
        let mutable experimentIndex = 0
        let mutable scanIndex       = 0            
        this.ParseBySpectrumID(spectrumID, & sampleIndex, & experimentIndex, & scanIndex)
        let sample  = batch.GetSample(sampleIndex).MassSpectrometerSample
        let msExp   = sample.GetMSExperiment(experimentIndex)
        msExp.Details.IDAType

    member this.GetMassRangeInfo(spectrumID) =
        this.RaiseDisposed()
        let mutable sampleIndex     = 0
        let mutable experimentIndex = 0
        let mutable scanIndex       = 0            
        this.ParseBySpectrumID(spectrumID, & sampleIndex, & experimentIndex, & scanIndex)
        let sample  = batch.GetSample(sampleIndex).MassSpectrometerSample
        let msExp   = sample.GetMSExperiment(experimentIndex)
        msExp.Details.MassRangeInfo

    member this.GetSampleState(runID) =
        this.RaiseDisposed()
        let mutable sampleIndex = 0          
        this.ParseByRunID(runID, & sampleIndex)
        let sample      = batch.GetSample(sampleIndex).MassSpectrometerSample
        sample.SampleState

    member this.GetSampleLocation(runID) =
        this.RaiseDisposed()
        let mutable sampleIndex = 0          
        this.ParseByRunID(runID, & sampleIndex)
        let sample      = batch.GetSample(sampleIndex).MassSpectrometerSample
        sample.Sample.SampleLocator.ContainerPath

    member this.GetSampleType(runID) =
        this.RaiseDisposed()
        let mutable sampleIndex = 0          
        this.ParseByRunID(runID, & sampleIndex)
        let sample      = batch.GetSample(sampleIndex).MassSpectrometerSample
        sample.Sample.Details.SampleType

    member this.GetExtraProperties(runID) =
        this.RaiseDisposed()
        let mutable sampleIndex = 0          
        this.ParseByRunID(runID, & sampleIndex)
        let sample      = batch.GetSample(sampleIndex).MassSpectrometerSample
        sample.Sample.Details.ExtraProperties

    member this.GetCalibration(spectrumID) =
        this.RaiseDisposed()
        let mutable sampleIndex     = 0
        let mutable experimentIndex = 0
        let mutable scanIndex       = 0            
        this.ParseBySpectrumID(spectrumID, & sampleIndex, & experimentIndex, & scanIndex)
        let sample  = batch.GetSample(sampleIndex).MassSpectrometerSample
        let msExp   = sample.GetMSExperiment(experimentIndex)
        let msSpec  = msExp.GetMassSpectrum(scanIndex)
        msSpec.Info.Calibration

    member this.GetCollisionEnergy(spectrumID) =
        this.RaiseDisposed()
        let mutable sampleIndex     = 0
        let mutable experimentIndex = 0
        let mutable scanIndex       = 0            
        this.ParseBySpectrumID(spectrumID, & sampleIndex, & experimentIndex, & scanIndex)
        let sample  = batch.GetSample(sampleIndex).MassSpectrometerSample
        let msExp   = sample.GetMSExperiment(experimentIndex)
        let msSpec  = msExp.GetMassSpectrum(scanIndex)
        msSpec.Info.CollisionEnergy

    member this.GetCentroidMode(spectrumID) =
        this.RaiseDisposed()
        let mutable sampleIndex     = 0
        let mutable experimentIndex = 0
        let mutable scanIndex       = 0            
        this.ParseBySpectrumID(spectrumID, & sampleIndex, & experimentIndex, & scanIndex)
        let sample  = batch.GetSample(sampleIndex).MassSpectrometerSample
        let msExp   = sample.GetMSExperiment(experimentIndex)
        let msSpec  = msExp.GetMassSpectrum(scanIndex)
        msSpec.Info.CentroidMode

    member this.GetParentChargeState(spectrumID) =
        this.RaiseDisposed()
        let mutable sampleIndex     = 0
        let mutable experimentIndex = 0
        let mutable scanIndex       = 0            
        this.ParseBySpectrumID(spectrumID, & sampleIndex, & experimentIndex, & scanIndex)
        let sample  = batch.GetSample(sampleIndex).MassSpectrometerSample
        let msExp   = sample.GetMSExperiment(experimentIndex)
        let msSpec  = msExp.GetMassSpectrum(scanIndex)
        msSpec.Info.ParentChargeState

