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
//#r @"../MzIO.Thermo\bin\Release\net451\ThermoFisher.CommonCore.BackgroundSubtraction.dll"
//#r @"../MzIO.Thermo\bin\Release\net451\ThermoFisher.CommonCore.Data.dll"
//#r @"../MzIO.Thermo\bin\Release\net451\ThermoFisher.CommonCore.MassPrecisionEstimator.dll"
//#r @"../MzIO.Thermo\bin\Release\net451\ThermoFisher.CommonCore.RawFileReader.dll"


open System
open System.Collections.Generic
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open MzIO.Binary
open MzIO.Wiff
open MzIO.SQLReader
open MzIO.MetaData.ParamEditExtension
open MzIO.MetaData.PSIMSExtension
open MzIO.Model
open MzIO.Model.CvParam
open MzIO.MetaData.UO.UO
open MzIO.Processing.MzIOLinq
open MzIO.Json
open MzIO.Bruker
open System.Runtime.InteropServices
//open ThermoFisher
//open ThermoFisher.CommonCore
//open ThermoFisher.CommonCore.RawFileReader
//open ThermoFisher.CommonCore.RawFileReader.Writers
//open ThermoFisher.CommonCore.Data
//open ThermoFisher.CommonCore.Data.Business
//open ThermoFisher.CommonCore.Data.FilterEnums
//open ThermoFisher.CommonCore.Data.Interfaces
//open ThermoFisher.CommonCore.BackgroundSubtraction
//open ThermoFisher.CommonCore.MassPrecisionEstimator

//let test = Software()
//let tests = SoftwareList()
//let paramValue = ParamValue.CvValue("value")
//let paramUnit = ParamValue.WithCvUnitAccession("value", "unit")

//test
//tests.Add(test)
//tests.Item("id")

//test.AddCvParam(CvParam.CvParam<string>("test", paramUnit))
//test.AddCvParam(CvParam.CvParam<string>("value", paramValue))
//test.AddCvParam(CvParam.UserParam<string>("testI", paramValue))


let fileDir             = __SOURCE_DIRECTORY__
let licensePath         = @"C:\Users\Patrick\source\repos\MzLiteFSharp\src\MzLiteFSharp.Wiff\License\Clearcore2.license.xml"

let wiffTestFileStudent = @"C:\Users\Student\OneDrive\MP_Biotech\VP_Timo\MassSpecFiles\wiffTestFiles\20171129 FW LWagg001.wiff"
let mzIOFileStudent     = @"C:\Users\Student\source\repos\wiffTestFiles\20171129 FW LWagg001.wiff.mzIO"

let jonMzIO             = @"C:\Users\jonat\OneDrive\MP_Biotech\VP_Timo\MassSpecFiles\test180807_Cold1_2d_GC8_01_8599.mzIO"
let jonWiff             = @"C:\Users\jonat\OneDrive\MP_Biotech\VP_Timo\MassSpecFiles\20180301_MS_JT88mutID122.wiff"

let wiffTestPaeddetor   = @"D:\Users\Patrick\Desktop\BioInformatik\MzLiteTestFiles\WiffTestFiles\20180301_MS_JT88mutID122.wiff"
let paddeTestPath       = @"D:\Users\Patrick\Desktop\BioInformatik\MzLiteTestFiles\WiffTestFiles\20180301_MS_JT88mutID122.wiff.mzIO"

let wiffTestUni         = @"C:\Users\Student\source\repos\wiffTestFiles\20171129 FW LWagg001.wiff"
let uniTestPath         = @"C:\Users\Student\source\repos\wiffTestFiles\20171129 FW LWagg001.wiff.mzIO"

let bafTestFile         = @"C:\Users\Student\source\repos\wiffTestFiles\Bruker\170922_4597.d\analysis.baf" 

let mzIOFSharpDBPath  = @"C:\Users\Student\source\repos\wiffTestFiles\Databases\MzLiteFSHarpLWagg001.mzIO"


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


//let wiffFileReader = new WiffFileReader(wiffTestPaeddetor, licensePath)
//let massSpectra = 
//    wiffFileReader.Model.Runs.GetProperties false
//    |> Seq.map (fun run -> wiffFileReader.ReadMassSpectra run.Key)
//    |> Seq.head
//    |> Array.ofSeq

//let massSpectrum = massSpectra.[0]

//let peaks = wiffFileReader.ReadSpectrumPeaks massSpectrum.ID

//massSpectrum.GetProperties false
//let peak = (Array.ofSeq peaks.Peaks).[0]
//peak

//let model = wiffFileReader.CreateDefaultModel()

//model.Runs.GetProperties false

//let testii = (new MzIOSQL(paddeTestPath))
//let testiii = testii.MzIOSQL()

//let testVI = testii.BeginTransaction()
//testii.Insert("meh", massSpectra.[2], peaks)
//testii.Insert("RunTest", massSpectra.[3], peaks)
//let testIIII = testii.ReadMassSpectra("meh") |> Array.ofSeq
//let testV = testii.ReadMassSpectra("RunTest") |> Array.ofSeq
//testVI.Commit()
//testVI.Dispose()
//testIIII.[0]
//testV.[0]

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

let getWiffFileReader (path:string) =
    new WiffFileReader(path, licensePath)

let getMassSpectra (wiffFileReader:WiffFileReader) =
    wiffFileReader.Model.Runs.GetProperties false
    |> Seq.collect (fun (run:KeyValuePair<string, obj>) -> wiffFileReader.ReadMassSpectra run.Key)

let getPeak1DArrays (wiffFileReader:WiffFileReader) =
    getMassSpectra wiffFileReader
    |> Seq.map (fun spectrum -> wiffFileReader.ReadSpectrumPeaks spectrum.ID)

let getMzIOHelper (path:string) (compressionType:BinaryDataCompressionType) =
    let wiffFileReader = new WiffFileReader(path, licensePath)
    let runIDMassSpectra =
        wiffFileReader.Model.Runs.GetProperties false
        |> Seq.map (fun (run:KeyValuePair<string, obj>) -> run.Key, wiffFileReader.ReadMassSpectra run.Key)
    let tmp =
        runIDMassSpectra
        |> Seq.map (fun (runID, massSpectra) ->
            massSpectra
            |> Seq.map (fun spectrum -> (wiffFileReader.ReadSpectrumPeaks spectrum.ID))
            |> Seq.map (fun peak -> peak.CompressionType <- compressionType
                                    peak)
            |> createMzIOHelper runID path massSpectra
            )
        |> Seq.head
    tmp
    
let insertWholeFileIntoDB (helper:MzIOHelper) =
    let mzIOSQL = new MzIOSQL(helper.Path + ".mzIO")
    let bn = mzIOSQL.BeginTransaction()
    Seq.map2 (fun (spectrum:MzIO.Model.MassSpectrum) (peak:Peak1DArray) -> mzIOSQL.Insert(helper.RunID, spectrum, peak)) helper.MassSpectrum helper.Peaks
    |> Seq.length |> ignore
    bn.Commit()
    bn.Dispose()
  
let insertIntoDB (amount:int) (helper:MzIOHelper) =
    let mzIOSQL = new MzIOSQL(helper.Path + ".mzIO")
    let bn = mzIOSQL.BeginTransaction()
    Seq.map2 (fun (spectrum:MzIO.Model.MassSpectrum) (peak:Peak1DArray) -> mzIOSQL.Insert(helper.RunID, spectrum, peak)) (Seq.take amount helper.MassSpectrum) (Seq.take amount helper.Peaks)
    |> Seq.length |> ignore
    bn.Commit()
    bn.Dispose()

let getSpectrum (path:string) (spectrumID:string) =
    let mzIOSQL = new MzIOSQL(path)
    let bn = mzIOSQL.BeginTransaction()
    mzIOSQL.ReadMassSpectrum spectrumID

let getSpectra (path:string) (helper:MzIOHelper) =
    let mzIOSQL = new MzIOSQL(path)
    let bn = mzIOSQL.BeginTransaction()
    let tmp = 
        mzIOSQL.ReadMassSpectra helper.RunID
        |> List.ofSeq
    bn.Commit()
    bn.Dispose()
    tmp

let getSpectrumPeaks (path:string) (spectrumID:string) =

    let mzIOSQL = new MzIOSQL(path)

    let bn = mzIOSQL.BeginTransaction()

    mzIOSQL.ReadSpectrumPeaks spectrumID


#time
let wiffFileReader =
    getWiffFileReader wiffTestPaeddetor

let massSpectra =
    getMassSpectra wiffFileReader

let helper = getMzIOHelper wiffTestPaeddetor BinaryDataCompressionType.NoCompression

let peak1DArrays = getPeak1DArrays wiffFileReader

let insertDB =
    getMzIOHelper wiffTestPaeddetor BinaryDataCompressionType.NoCompression
    |> (fun wiffFileReader -> insertIntoDB 100 wiffFileReader)

//let spectrum =
//    getSpectrum paddeTestPath "sample=0 experiment=0 scan=0"

//let spectra =
//    getMzIOHelper wiffTestPaeddetor BinaryDataCompressionType.ZLib
//    |> getSpectra paddeTestPath 

//let peaks =
//    //spectra
//    //|> Seq.map (fun item -> getSpectrumPeaks paddeTestPath item.ID)
//    getSpectrumPeaks paddeTestPath "sample=0 experiment=0 scan=0"

//Seq.length peaks


//for i in peaks do
//    for peak in i.Peaks do
//        printfn "%f %f" peak.Mz peak.Intensity

//let tmp =
//    helper.MassSpectrum |> List.ofSeq
//    |> List.head


//let mutable idx = 0
//tmp.TryGetMsLevel(& idx)
//idx
//tmp.TryGetValue(PSIMS_Spectrum.MsLevel).Value


//let y = new CvParam<int>("test", ParamValue.WithCvUnitAccession(5, "SOME TEST"))
//let yIII = new CvParam<int>("testI")
//let text = MzIOJson.ToJson(y)
//MzIOJson.ToJson(yIII)
//MzIOJson.FromJson(text) :> CvParam<IConvertible>
////MzIOJson.ToJson(yIII)

//MzIOJson.ToJson(ParamValue.WithCvUnitAccession(5, "SOME TEST").ToString())
//JsonConvert.SerializeObject(ParamValue.WithCvUnitAccession(5, "SOME TEST").ToString())

//"WithCvUnitAccession (5,\"SOME TEST\")"

//let cvTest = sprintf "{\"%s\":\"%s\",%s}" "$id" "1" "\"Type\":\"WithCvUnitAccession\",\"Values\":[5,\"SOME TEST\"]"
//let jsonX = JsonConvert.DeserializeObject<JObject>(cvTest)
//jsonX.ToString()
//jsonX.["Type"]
//jsonX.["Values"] :?> JArray



//"WithCvUnitAccession".Length
//"CvValue".Length
//let tmp = new MassSpectrum()

//tmp.AddCvParam(y)
//tmp.AddCvParam(yIII)
//tmp.AddCvParam(new CvParam<string>("testII", ParamValue.WithCvUnitAccession(null, "SOME TEST")))
//tmp.AddCvParam(new CvParam<string>("testIII", ParamValue.CvValue("SOME VAlue")))
//tmp.TryGetValue("test")
//tmp.TryGetValue("testI")
//tmp.TryGetValue("testII")
//tmp.TryGetValue("testIII")

//let tmpIII = MzIOJson.FromJson(MzIOJson.ToJson(tmp)) :> MassSpectrum

//for i in tmpIII.GetDynamicMemberNames() do
//    printfn "%s" i

//tmpIII.TryGetValue("test")
//tmpIII.TryGetValue("testI")
//tmpIII.TryGetValue("testII")
//tmpIII.TryGetValue("testIII")

////let text2 = MzIOJson.ToJson(tmp)
////let tmp2 = MzIOJson.FromJson(text2) :> DynamicObj

////MzIOJson.ToJson(y)
////|> (fun item -> MzIOJson.FromJson(item)) :> CvParam<int>

////let x = ((Seq.head (tmp2.GetProperties false)).Value.ToString())
////MzIOJson.FromJson(x) :> CvParam<int64>

////let jObj = tmp2.TryGetValue("test").Value :?> JObject

////MzIOJson.FromJson(jObj.ToString()) :> CvParam<int>
////jObj.First.ToString()
//////MzIOJson.FromJson(jObj.First.Next.ToString())

////jObj.ToString()

////let xI = jObj.["Value"] :?> JObject

////xI.["Case"].ToString()
////let yI = xI.["Fields"]

////y.ToString()

////xI.Count
////xI.First.ToString()
////yI.First.["Case"].ToString()
////yI.First.["Fields"].First.ToString()
////yI.First.["Fields"].Last.ToString()
////xI.Last.ToString()

////deSerializeInPlaceParamValues tmp2

////tmp2.TryGetValue("test")
////tmp2.TryGetValue("testI")
////tmp2.TryGetValue("testII")
////tmp2.TryGetValue("testIII")


////tmp.AddCvParam y
////tmp.SetCvParam("test").UO_Liter()
////let zy = tryGetCvUnitAccession (tmp.TryGetTypedValue<CvParam<IConvertible>>("test").Value)
////zy


////JObject.Parse(text).First.Next.ToString()
////JObject.Parse(text).First.Next.Next.ToString()


//////let dbPath = @"C:\Users\Student\OneDrive\MP_Biotech\VP_Timo\MassSpecFiles\wiffTestFiles\20171129 FW LWagg001_testMzIO.mzIO"

let mzsqlreader = new MzIOSQL(paddeTestPath)

let tr = mzsqlreader.BeginTransaction()

let tmpX = mzsqlreader.ReadMassSpectra("sample=0") |> List.ofSeq |> List.head

let mutable idx1 = 0
tmpX.TryGetMsLevel(& idx1)
idx1

tmpX.TryGetValue(PSIMS_Spectrum.MsLevel)

//tmpX.TryGetTypedValue<CvParam<IConvertible>>(PSIMS_Spectrum.MsLevel)

tmpX.Scans.GetProperties true

let rtIndexEntry = mzsqlreader.BuildRtIndex("sample=0")

let rtProfile = mzsqlreader.RtProfile (rtIndexEntry, (new MzIO.Processing.RangeQuery(1., 300., 600.)), (new MzIO.Processing.RangeQuery(1., 300., 600.)))

//let tmpXY = (new Scan())
//tmpXY.AddCvParam(new CvParam<string>("Test)"))

//let scan = Seq.head (tmpX.Scans.GetProperties false)
//MzIOJson.FromJson(scan.Value.ToString()) :> Scan

//fileDir + "..\baf2sqlLib\win32/baf2sql_c.dll"

////let test = new MassSpectrum()
////let scanList = (new ScanList())
////scanList.Add(new Scan())
////test.Scans.Add(new Scan())
////test.Scans.Count
////let testJson = MzIOJson.ToJson(test)
////let testUnJson = MzIOJson.FromJson(testJson) :> MassSpectrum
//////MzIOJson.ToJson(testUnJson)
////testUnJson.GetProperties false
////testUnJson.Scans.Count
//////let testJsonTextArray = (Seq.head (testUnJson.GetProperties false)).ToString()
//////JArray.Parse(testJsonTextArray)
 

//////let xI =
//////    JsonConvert.SerializeObject(test, MzIOJson.jsonSettings)
//////    |> (fun item -> JsonConvert.DeserializeObject<MassSpectrum>(item))

//////xI.Scans.Count

//let brukerReader = new BafFileReader(bafTestFile)

//open ThermoFisher
//open ThermoFisher.CommonCore
//open ThermoFisher.CommonCore.RawFileReader
//open ThermoFisher.CommonCore.RawFileReader.Writers
//open ThermoFisher.CommonCore.Data
//open ThermoFisher.CommonCore.Data.Business
//open ThermoFisher.CommonCore.Data.FilterEnums
//open ThermoFisher.CommonCore.Data.Interfaces
//open ThermoFisher.CommonCore.BackgroundSubtraction
//open ThermoFisher.CommonCore.MassPrecisionEstimator

//let rawtestFile = "D:\Users\Patrick\Desktop\BioInformatik\MzIOTestFiles\RawTestFiles\small.RAW"

////ThermoFisher.CommonCore.Data.Business.IScanReader

//let header = RawFileReaderAdapter.FileHeaderFactory(rawtestFile)
//let rawFileFactory = RawFileReaderAdapter.FileFactory(rawtestFile)
//let instruments = InstrumentMethodFileReader.OpenMethod(rawtestFile)
//let processings = ProcessingMethodFileReader.OpenProcessingMethod(rawtestFile)
//let sequenceFiles = SequenceFileReader.OpenSequence(rawtestFile)
