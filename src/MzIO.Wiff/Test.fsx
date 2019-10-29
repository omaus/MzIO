﻿
#r @"../MzIO.SQL\bin\Release\net45/System.Data.SQLite.dll"
#r @"../MzIO.Wiff\bin\Release\net45\Clearcore2.Muni.dll"
#r @"../MzIO.Wiff\bin\Release\net45\Clearcore2.Data.dll"
#r @"../MzIO.Wiff\bin\Release\net45\Clearcore2.Data.CommonInterfaces.dll"
#r @"../MzIO.Wiff\bin\Release\net45\Clearcore2.Data.AnalystDataProvider.dll"
#r @"../MzIO.Wiff\bin\Release\net45/Newtonsoft.Json.dll"
#r @"../MzIO.Wiff\bin\Release\net45/MzIO.dll"
#r @"../MzIO.Wiff\bin\Release\net45\MzIO.Wiff.dll"
#r @"../MzIO.SQL\bin\Release\net45\MzIO.SQL.dll"
#r @"../MzIO.Processing\bin\Release\net45\MzIO.Processing.dll"
#r @"../MzIO.Bruker\bin\Release\net45\MzIO.Bruker.dll"
#r @"../MzIO.MzML\bin\Release\net45\MzIO.MzML.dll"
#r @"../MzIO.Thermo\bin\Release\net451\ThermoFisher.CommonCore.BackgroundSubtraction.dll"
#r @"../MzIO.Thermo\bin\Release\net451\ThermoFisher.CommonCore.Data.dll"
#r @"../MzIO.Thermo\bin\Release\net451\ThermoFisher.CommonCore.MassPrecisionEstimator.dll"
#r @"../MzIO.Thermo\bin\Release\net451\ThermoFisher.CommonCore.RawFileReader.dll"
#r @"../MzIO.Thermo\bin\Release\net451\MzIO.Thermo.dll"


open System
open System.Data
open System.Data.SQLite
open System.IO
open System.Threading.Tasks
open System.Xml
open System.Collections.Generic
open System.Runtime.InteropServices
open System.IO.Compression
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open MzIO.Binary
open MzIO.Wiff
open MzIO.MzSQL
open MzIO.MetaData.ParamEditExtension
open MzIO.MetaData.PSIMSExtension
open MzIO.Model
open MzIO.Model.CvParam
open MzIO.MetaData.UO.UO
open MzIO.Processing.MzIOLinq
open MzIO.Json
open MzIO.Bruker
open MzIO.IO.MzML
open MzIO.IO.MzML
open MzIO.IO
open MzIO.Thermo
open MzIO.Processing.Indexer
open MzIO.Processing.MassSpectrum


let fileDir             = __SOURCE_DIRECTORY__
let licensePath         = @"C:\Users\Student\source\repos\MzLiteFSharp\src\MzLiteFSharp.Wiff\License\Clearcore2.license.xml"
let licenseHome         = @"C:\Users\Patrick\source\repos\MzLiteFSharp\src\MzLiteFSharp.Wiff\License\Clearcore2.license.xml"

let wiffTestFileStudent = @"C:\Users\Student\OneDrive\MP_Biotech\VP_Timo\MassSpecFiles\wiffTestFiles\20171129 FW LWagg001.wiff"
let mzIOFileStudent     = @"C:\Users\Student\source\repos\wiffTestFiles\20171129 FW LWagg001.wiff.mzIO"

let jonMzIO             = @"C:\Users\jonat\OneDrive\MP_Biotech\VP_Timo\MassSpecFiles\test180807_Cold1_2d_GC8_01_8599.mzIO"
let jonWiff             = @"C:\Users\jonat\OneDrive\MP_Biotech\VP_Timo\MassSpecFiles\20180301_MS_JT88mutID122.wiff"

let wiffTestPaeddetor   = @"D:\Users\Patrick\Desktop\BioInformatik\MzLiteTestFiles\WiffTestFiles\20180301_MS_JT88mutID122.wiff"
let paddeTestPath       = @"D:\Users\Patrick\Desktop\BioInformatik\MzLiteTestFiles\WiffTestFiles\20180301_MS_JT88mutID122.wiff.mzIO"

let mzIOFSharpDBPath    = @"C:\Users\Student\source\repos\wiffTestFiles\Databases\MzLiteFSHarpLWagg001.mzIO"


type MzIOHelper =
    {
        RunID           : string
        MassSpectrum    : seq<MzIO.Model.MassSpectrum>
        Peaks           : seq<Peak1DArray>
        Path            : string
    }

let createMzIOHelper (runID:string) (path:string) (spectrum:seq<MzIO.Model.MassSpectrum>) (peaks:seq<Peak1DArray>) =
    {
        MzIOHelper.RunID          = runID
        MzIOHelper.MassSpectrum   = spectrum
        MzIOHelper.Peaks          = peaks
        MzIOHelper.Path           = path
    }

//let wiffFilePaths =
//    [
//        @"C:\Users\Student\source\repos\wiffTestFiles\20171129 FW LWagg001.wiff"
//        @"C:\Users\Student\source\repos\wiffTestFiles\20171129 FW LWagg002.wiff"
//        @"C:\Users\Student\source\repos\wiffTestFiles\20171129 FW LWagg003.wiff"
//        @"C:\Users\Student\source\repos\wiffTestFiles\20171129 FW LWagg004.wiff"
//        @"C:\Users\Student\source\repos\wiffTestFiles\20171129 FW LWagg005.wiff"
//        @"C:\Users\Student\source\repos\wiffTestFiles\20171129 FW LWagg006.wiff"
//        @"C:\Users\Student\source\repos\wiffTestFiles\20171129 FW LWagg007.wiff"
//        @"C:\Users\Student\source\repos\wiffTestFiles\20171129 FW LWagg008.wiff"
//        @"C:\Users\Student\source\repos\wiffTestFiles\20171129 FW LWagg009.wiff"
//        @"C:\Users\Student\source\repos\wiffTestFiles\20171129 FW LWagg010.wiff"
//        @"C:\Users\Student\source\repos\wiffTestFiles\20171129 FW LWagg011.wiff"
//        //@"C:\Users\Student\source\repos\wiffTestFiles\20171129 FW LWagg012.wiff"
//    ]

#time
let rand = new System.Random()

let swap (a: _[]) x y =
    let tmp = a.[x]
    a.[x] <- a.[y]
    a.[y] <- tmp

// shuffle an array (in-place)
let shuffle a =
    Array.iteri (fun i _ -> swap a i (rand.Next(i, Array.length a))) a


let wiffTestUni     = @"C:\Users\Student\source\repos\wiffTestFiles\WiffFiles\20171129 FW LWagg001.wiff"
let wiffTestHome    = @"D:\Users\Patrick\Desktop\BioInformatik\MzLiteTestFiles\WiffTestFiles\20180301_MS_JT88mutID122.wiff"
let mzMLOfWiffUni   = @"C:\Users\Student\source\repos\wiffTestFiles\WiffFiles\20171129 FW LWagg001.mzML"

let bafTestUni      = @"C:\Users\Student\source\repos\wiffTestFiles\Bruker\170922_4597.d\analysis.baf"
let bafTestHome     = @"D:\Users\Patrick\Desktop\BioInformatik\MzLiteTestFiles\BafTestFiles\analysis.baf"
let bafMzMLFile     = @"C:\Users\Student\source\repos\wiffTestFiles\Bruker\170922_4597.mzML"

let thermoUni       = @"C:\Users\Student\source\repos\wiffTestFiles\Thermo\data02.RAW"
let termoMzML       = @"C:\Users\Student\source\repos\wiffTestFiles\Thermo\data02.mzML"

//let mzMLHome        = @"D:\Users\Patrick\Desktop\BioInformatik\MzLiteTestFiles\MzMLTestFiles\tiny.pwiz.1.1.txt"
//let mzMLHome    = @"D:\Users\Patrick\Desktop\BioInformatik\MzLiteTestFiles\MzMLTestFiles\small_miape.pwiz.1.1.txt"

let wiffReader          = new WiffFileReader(wiffTestHome, licenseHome)
//let wiffMzML            = new MzMLReader(mzMLOfWiffUni)

//let bafReader           = new BafFileReader(bafTestHome)
//let bafMzMLReader       = new MzMLReader(bafMzMLFile)

//let thermoReader        = new ThermoRawFileReader(thermoUni)
//let thermoMzMLReader    = new MzMLReader(termoMzML)

//let mzMLReader          = new MzMLReader(@"D:\Users\Patrick\Desktop\BioInformatik\MzLiteTestFiles\WiffTestFiles\20180301_MS_JT88mutID122.mzML")

let getSpectras (reader:#IMzIODataReader) =
    reader.Model.Runs.GetProperties false
    |> Seq.collect (fun run -> reader.ReadMassSpectra (run.Value :?> Run).ID)

//let rtIndexEntry = wiffReader.BuildRtIndex("sample=0")
//let rtProfile = wiffReader.RtProfile (rtIndexEntry, (new MzIO.Processing.RangeQuery(1., 300., 600.)), (new MzIO.Processing.RangeQuery(1., 300., 600.)))

//let mzSQLNoCompression  = new MzSQL(wiffTestHome + "NoCompression.mzIO")
//let mzSQLZLib           = new MzSQL(wiffTestHome + "ZLib.mzIO")
//let mzSQLNumPress       = new MzSQL(wiffTestHome + "NumPress.mzIO")
//let mzSQLNumPressZLib   = new MzSQL(wiffTestHome + "NumPressZLib.mzIO")

//let mzMLNoCompression   = new MzMLWriter(wiffTestHome + "NoCompression.mzml")
//let mzMLZLib            = new MzMLWriter(wiffTestHome + "ZLib.mzml")
//let mzMLNumPress        = new MzMLWriter(wiffTestHome + "NumPress.mzml")
//let mzMLNumPressZLib    = new MzMLWriter(wiffTestHome + "NumPressZLib.mzml")

//let mzMLReaderNoCompression = new MzMLReader(wiffTestHome + "NoCompression.mzml")
//let mzMLReaderZLib          = new MzMLReader(wiffTestHome + "ZLib.mzml")
//let mzMLReaderNumPress      = new MzMLReader(wiffTestHome + "NumPress.mzml")
//let mzMLReaderNumPressZLib  = new MzMLReader(wiffTestHome + "NumPressZLib.mzml")

let spectra =
    wiffReader.Model.Runs.GetProperties false
    |> Seq.map (fun item -> item.Value :?> Run)
    |> Seq.head
    |> (fun run -> wiffReader.ReadMassSpectra run.ID)
    |> Array.ofSeq
    |> Array.ofSeq
    |> Array.filter (fun x -> MzIO.Processing.MassSpectrum.getMsLevel x = 1)

//let precursor = new Precursor()
//let selectedIon = new SelectedIon()
//selectedIon.SetSelectedIonMz(4.0)
//precursor.SelectedIons.Add(selectedIon.ToString(), selectedIon)
//spectra
//|> Array.map (fun spectrum -> 
//    spectrum.Precursors.Add(precursor.ToString(), precursor))

//let peaks =
//    wiffReader.ReadSpectrumPeaks "run_1"
//    |> Seq.length

//wiffReader.Model.Runs.GetProperties false
//|> Seq.map (fun item -> item.Value :?> Run)
//|> Seq.head
//|> (fun run -> wiffReader.ReadMassSpectra run.ID)
//|> Seq.length

//shuffle spectra

//spectra
//|> Seq.map (fun spectrum -> wiffReader.ReadMassSpectrum spectrum.ID)
//|> Seq.length

//spectra
//|> Seq.map (fun spectrum -> wiffReader.ReadSpectrumPeaks spectrum.ID)
//|> Seq.length
//mzSQLNoCompression.Open()
//let tr = mzSQLNoCompression.cn.BeginTransaction()
//insertMSSpectraBy insertMSSpectrum mzSQLNoCompression "run_1" wiffReader tr BinaryDataCompressionType.NoCompression spectra

//mzSQLNoCompression.insertMSSpectraBy (mzSQLNoCompression.insertMSSpectrum)  "run_1" wiffReader BinaryDataCompressionType.NoCompression spectra
//mzSQLZLib.insertMSSpectraBy          (mzSQLZLib.insertMSSpectrum)           "run_1" wiffReader BinaryDataCompressionType.ZLib          spectra
//mzSQLNumPress.insertMSSpectraBy      (mzSQLNumPress.insertMSSpectrum)       "run_1" wiffReader BinaryDataCompressionType.NumPress      spectra
//mzSQLNumPressZLib.insertMSSpectraBy  (mzSQLNumPressZLib.insertMSSpectrum)   "run_1" wiffReader BinaryDataCompressionType.NumPressZLib  spectra

//shuffle spectra

//mzSQLNoCompression.ReadMassSpectra "run_1"
//|> Seq.length
//spectra
//|> Seq.map (fun spectrum -> mzSQLNoCompression.ReadMassSpectrum spectrum.ID)
//|> Seq.length
//spectra
//|> Seq.map (fun spectrum -> mzSQLNoCompression.ReadSpectrumPeaks spectrum.ID)
//|> Seq.length

////mzSQLZLib.ReadMassSpectra "run_1"
////|> Seq.length
//spectra
//|> Seq.map (fun spectrum -> mzSQLZLib.ReadMassSpectrum spectrum.ID)
//|> Seq.length
//spectra
//|> Seq.map (fun spectrum -> mzSQLZLib.ReadSpectrumPeaks spectrum.ID)
//|> Seq.length

////mzSQLNumPress.ReadMassSpectra "run_1"
////|> Seq.length
//spectra
//|> Seq.map (fun spectrum -> mzSQLNumPress.ReadMassSpectrum spectrum.ID)
//|> Seq.length
//spectra
//|> Seq.map (fun spectrum -> mzSQLNumPress.ReadSpectrumPeaks spectrum.ID)
//|> Seq.length

////mzSQLNumPressZLib.ReadMassSpectra "run_1"
////|> Seq.length
//spectra
//|> Seq.map (fun spectrum -> mzSQLNumPressZLib.ReadMassSpectrum spectrum.ID)
//|> Seq.length
//spectra
//|> Seq.map (fun spectrum -> mzSQLNumPressZLib.ReadSpectrumPeaks spectrum.ID)
//|> Seq.length

//let instrument =
//    wiffReader.Model.Instruments.GetProperties false 
//    |> Seq.head 
//    |> (fun item -> item.Value :?> Instrument)

//let software = 
//    wiffReader.Model.Softwares.GetProperties false 
//    |> Seq.head 
//    |> (fun item -> item.Value :?> Software)
//instrument.Software

//mzMLNoCompression.insertMSSpectraBy   (mzMLNoCompression.insertMSSpectrum)  "run_1" wiffReader BinaryDataCompressionType.NoCompression spectra
//mzMLZLib.insertMSSpectraBy            (mzMLZLib.insertMSSpectrum)           "run_1" bafReader BinaryDataCompressionType.ZLib          spectra
//mzMLNumPress.insertMSSpectraBy        (mzMLNumPress.insertMSSpectrum)       "run_1" bafReader BinaryDataCompressionType.NumPress      spectra
//mzMLNumPressZLib.insertMSSpectraBy    (mzMLNumPressZLib.insertMSSpectrum)   "run_1" bafReader BinaryDataCompressionType.NumPressZLib  spectra

//let testSoftware = new Software("Test")
//let testInStrument = new Instrument("Test", testSoftware)
//testInStrument.Software

//mzMLReaderNoCompression.ReadMassSpectra "run_1"
//|> Seq.length
//mzMLReaderNoCompression.ReadMassSpectra "run_1"
//|> Seq.map (fun spectrum -> mzMLReaderNoCompression.ReadMassSpectrum spectrum.ID)
//|> Seq.length
//mzMLReaderNoCompression.ReadMassSpectra "run_1"
//|> Seq.map (fun spectrum -> mzMLReaderNoCompression.ReadSpectrumPeaks spectrum.ID)
//|> Seq.length

//mzMLReaderZLib.ReadMassSpectra "run_1"
//|> Seq.length
//mzMLReaderZLib.ReadMassSpectra "run_1"
//|> Seq.map (fun spectrum -> mzMLReaderZLib.ReadMassSpectrum spectrum.ID)
//|> Seq.length
//mzMLReaderZLib.ReadMassSpectra "run_1"
//|> Seq.map (fun spectrum -> mzMLReaderZLib.ReadSpectrumPeaks spectrum.ID)
//|> Seq.length

//mzMLReaderNumPress.ReadMassSpectra "run_1"
//|> Seq.length
//mzMLReaderNumPress.ReadMassSpectra "run_1"
//|> Seq.map (fun spectrum -> mzMLReaderNumPress.ReadMassSpectrum spectrum.ID)
//|> Seq.length
//mzMLReaderNumPress.ReadMassSpectra "run_1"
//|> Seq.map (fun spectrum -> mzMLReaderNumPress.ReadSpectrumPeaks spectrum.ID)
//|> Seq.length

//mzMLReaderNumPressZLib.ReadMassSpectra "run_1"
//|> Seq.length
//mzMLReaderNumPressZLib.ReadMassSpectra "run_1"
//|> Seq.map (fun spectrum -> mzMLReaderNumPressZLib.ReadMassSpectrum spectrum.ID)
//|> Seq.length
//mzMLReaderNumPressZLib.ReadMassSpectra "run_1"
//|> Seq.map (fun spectrum -> mzMLReaderNumPressZLib.ReadSpectrumPeaks spectrum.ID)
//|> Seq.length


//mzSQLNoCompression  .BuildRtIndex ("run_1")
//mzSQLZLib           .BuildRtIndex ("run_1")
//mzSQLNumPress       .BuildRtIndex ("run_1")
//mzSQLNumPressZLib   .BuildRtIndex ("run_1")

//mzSQLNoCompression  .RtProfile  (mzSQLNoCompression.BuildRtIndex("run_1"), (new MzIO.Processing.RangeQuery(1., 0., 3000.)), (new MzIO.Processing.RangeQuery(1., 0., 3000.)))
//mzSQLZLib           .RtProfile  (mzSQLZLib.BuildRtIndex         ("run_1"), (new MzIO.Processing.RangeQuery(1., 0., 3000.)), (new MzIO.Processing.RangeQuery(1., 0., 3000.)))
//mzSQLNumPress       .RtProfile  (mzSQLNumPress.BuildRtIndex     ("run_1"), (new MzIO.Processing.RangeQuery(1., 0., 3000.)), (new MzIO.Processing.RangeQuery(1., 0., 3000.)))
//mzSQLNumPressZLib   .RtProfile  (mzSQLNumPressZLib.BuildRtIndex ("run_1"), (new MzIO.Processing.RangeQuery(1., 0., 3000.)), (new MzIO.Processing.RangeQuery(1., 0., 3000.)))

//mzMLReaderNoCompression  .BuildRtIndex ("run_1")
//mzMLReaderZLib           .BuildRtIndex ("run_1")
//mzMLReaderNumPress       .BuildRtIndex ("run_1")
//mzMLReaderNumPressZLib   .BuildRtIndex ("run_1")

//mzMLReaderNoCompression  .RtProfile  (mzMLReaderNoCompression  .BuildRtIndex("run_1"), (new MzIO.Processing.RangeQuery(1., 0., 3000.)), (new MzIO.Processing.RangeQuery(1., 0., 3000.)))
//mzMLReaderZLib           .RtProfile  (mzMLReaderZLib           .BuildRtIndex("run_1"), (new MzIO.Processing.RangeQuery(1., 0., 3000.)), (new MzIO.Processing.RangeQuery(1., 0., 3000.)))
//mzMLReaderNumPress       .RtProfile  (mzMLReaderNumPress       .BuildRtIndex("run_1"), (new MzIO.Processing.RangeQuery(1., 0., 3000.)), (new MzIO.Processing.RangeQuery(1., 0., 3000.)))
//mzMLReaderNumPressZLib   .RtProfile  (mzMLReaderNumPressZLib   .BuildRtIndex("run_1"), (new MzIO.Processing.RangeQuery(1., 0., 3000.)), (new MzIO.Processing.RangeQuery(1., 0., 3000.)))

//shuffle spectra

//let spectra100 =
//    spectra
//    |> Array.take 100

//let spectra1000 =
//    spectra
//    |> Array.take 1000

//let spectra2000 =
//    spectra
//    |> Array.take 2000

//let spectra3000 =
//    spectra
//    |> Array.take 3000

//let spectra4000 =
//    spectra
//    |> Array.take 4000

//let spectra5000 =
//    spectra
//    |> Array.take 5000

//let spectra10000 =
//    spectra
//    |> Array.take 10000

//let spectra20000 =
//    spectra
//    |> Array.take 20000

//let spectra30000 =
//    spectra
//    |> Array.take 30000

//let spectra40000 =
//    spectra
//    |> Array.take 40000

//let spectra50000 =
//    spectra
//    |> Array.take 50000

//let spectra60000 =
//    spectra
//    |> Array.take 60000

//let spectra70000 =
//    spectra
//    |> Array.take 70000

//spectra100
//|> Seq.map (fun spectrum -> mzSQLNoCompression.ReadSpectrumPeaks spectrum.ID)
//|> Seq.length

//spectra1000
//|> Seq.map (fun spectrum -> mzSQLNoCompression.ReadSpectrumPeaks spectrum.ID)
//|> Seq.length

//spectra2000
//|> Seq.map (fun spectrum -> mzSQLNoCompression.ReadSpectrumPeaks spectrum.ID)
//|> Seq.length

//spectra3000
//|> Seq.map (fun spectrum -> mzSQLNoCompression.ReadSpectrumPeaks spectrum.ID)
//|> Seq.length

//spectra4000
//|> Seq.map (fun spectrum -> mzSQLNoCompression.ReadSpectrumPeaks spectrum.ID)
//|> Seq.length

//spectra5000
//|> Seq.map (fun spectrum -> mzSQLNoCompression.ReadSpectrumPeaks spectrum.ID)
//|> Seq.length

//spectra10000
//|> Seq.map (fun spectrum -> mzSQLNoCompression.ReadSpectrumPeaks spectrum.ID)
//|> Seq.length

//spectra20000
//|> Seq.map (fun spectrum -> mzSQLNoCompression.ReadSpectrumPeaks spectrum.ID)
//|> Seq.length

//spectra30000
//|> Seq.map (fun spectrum -> mzSQLNoCompression.ReadSpectrumPeaks spectrum.ID)
//|> Seq.length

//spectra40000
//|> Seq.map (fun spectrum -> mzSQLNoCompression.ReadSpectrumPeaks spectrum.ID)
//|> Seq.length

//spectra50000
//|> Seq.map (fun spectrum -> mzSQLNoCompression.ReadSpectrumPeaks spectrum.ID)
//|> Seq.length

//spectra60000
//|> Seq.map (fun spectrum -> mzSQLNoCompression.ReadSpectrumPeaks spectrum.ID)
//|> Seq.length

//spectra70000
//|> Seq.map (fun spectrum -> mzSQLNoCompression.ReadSpectrumPeaks spectrum.ID)
//|> Seq.length

//spectra
//|> Seq.map (fun spectrum -> mzSQLNoCompression.ReadSpectrumPeaks spectrum.ID)
//|> Seq.length

//mzSQLNoCompression.BuildRtIndex ("run_1")
//mzSQLNoCompression.RtProfile (mzSQLNoCompression.BuildRtIndex("run_1"), (new MzIO.Processing.RangeQuery(1., 0., 3000.)), (new MzIO.Processing.RangeQuery(1., 0., 3000.)))

//let spectrum = spectra.[0]

//let sqlSqpctrum = mzSQLNoCompression.SelectMassSpectrum "sample=0 experiment=0 scan=0"

//spectrum.GetProperties false
//sqlSqpctrum.GetProperties false

//getScanTime spectrum
//getScanTime sqlSqpctrum

//let mutable msLevel = 0
//sqlSqpctrum.TryGetMsLevel(&msLevel)

//getPrecursorMZ spectrum
//getPrecursorMZ sqlSqpctrum

//spectrum.Precursors.Count()
//sqlSqpctrum.Precursors.Count()

//let tmp =
//    sqlSqpctrum.Precursors.GetProperties false
//    |> Seq.collect (fun item -> (item.Value :?> Precursor).SelectedIons.GetProperties false
//                                |> Seq.map (fun selectedIon -> 
//                                    selectedIon.Value :?> SelectedIon))

//(Seq.head tmp).GetProperties false

//spectra
//|> Seq.filter (fun item -> (getMsLevel item) = 2)
////|> Seq.map(fun item -> item.ID, getScanTime item)
//|> Seq.map (fun spectrum -> wiffReader.ReadSpectrumPeaks spectrum.ID)
//|> Seq.filter (fun peak -> peak.Peaks.Length <> 0)
//|> Seq.length



//wiffReader.GetXUnitOfChromatogram("sample=0 experiment=1 scan=0")
//wiffReader.GetYUnitOfChromatogram("sample=0 experiment=1 scan=0")

//wiffReader.GetXValuesOfChromatogram("sample=0 experiment=0 scan=0")
//wiffReader.GetYValuesOfChromatogram("sample=0 experiment=0 scan=0")

//wiffReader.GetXValuesOfChromatogram("sample=0 experiment=1 scan=0").Length
//wiffReader.GetYValuesOfChromatogram("sample=0 experiment=1 scan=0").Length

//wiffReader.GetXValuesOfChromatogram("sample=0 experiment=0 scan=1", 1)
//wiffReader.GetYValuesOfChromatogram("sample=0 experiment=0 scan=1", 1)

//wiffReader.GetXValuesOfChromatogram("sample=0 experiment=1 scan=1", 2).Length
//wiffReader.GetYValuesOfChromatogram("sample=0 experiment=1 scan=1", 2).Length

//wiffReader.GetXValuesOfChromatogram("sample=0 experiment=1 scan=1", 2) |> Array.sort
//wiffReader.GetYValuesOfChromatogram("sample=0 experiment=1 scan=2", 2) |> Array.filter (fun item -> item <> 0.) |> Array.length

let runID = 
    wiffReader.Model.Runs.GetProperties false
    |> Seq.map (fun item -> item.Value :?> Run)
    |> Seq.head

spectra.Length

//let peaks =
//    spectra
//    |> Seq.map (fun spectrum -> wiffReader.ReadSpectrumPeaks spectrum.ID)
//    |> Seq.length

1+1

//let test = wiffReader.GetChromatogramsOfMSLevel(runID.ID, 1)
let test2 = wiffReader.ReadSpectrumPeaks("sample=0 experiment=0 scan=0")
test2.Peaks.Length
wiffReader.GetTICOfRun(runID.ID)
wiffReader.GetTICOfSpectrum("sample=0 experiment=0 scan=0")
