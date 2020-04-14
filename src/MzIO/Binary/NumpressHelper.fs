﻿namespace NumpressHelper


open MSNumpressFSharp


module NumpressEncodingHelpers =

    type NumpressHelper =
        {
            Bytes               :   byte[]
            NumberEncodedBytes  :   int32
            OriginalDataLength  :   int32
        }

    let createNumpressHelper bytes neb odl =
        {
            NumpressHelper.Bytes                = bytes
            NumpressHelper.NumberEncodedBytes   = neb
            NumpressHelper.OriginalDataLength   = odl
        }


    ///This function takes an array of floats and returns a NumpressHelper containing the array of encoded bytes, number of encoded bytes and original data length.
    let encodePIC (array: float[]) =
        //empty array which gets filled by the encodePic function
        //maximal length of this array is original length * 5, after encoding only the used part of the array gets taken
        let (encodedByteArray: byte[]) =
            if array.Length < 2 then
                Array.zeroCreate (10)
            else
                Array.zeroCreate (array.Length * 5)
        //encoding happens here
        let encodeInt = Encode.encodePic(array, array.Length, encodedByteArray)
        //creates a NumpressHelper with the encoded byte array, the number of encoded bytes and original data length
        //only encodeInt + 5 fields of the byte array are taken, since the others are unused and not needed for decoding.
        createNumpressHelper (encodedByteArray |> Array.take (encodeInt + 5))  encodeInt array.Length


    let encodeLin (array: float[]) =
        //empty array which gets filled by the encodePic function
        //maximal length of this array is original length * 5, after encoding only the used part of the array gets taken
        let (encodedByteArray: byte[]) =
            if array.Length < 20 then
                Array.zeroCreate 200
            else
                Array.zeroCreate (array.Length * 5)
        //gives optimal fixed point for encoding. Can also be set by hand.
        let fixedPoint = Encode.optimalLinearFixedPointSave(array, array.Length)
        //encoding happens here
        let encodeInt = Encode.encodeLinear (array, array.Length, encodedByteArray, fixedPoint)
        //creates a NumpressHelper with the encoded byte array, the number of encoded bytes and original data length
        //only encodeInt + 5 fields of the byte array are taken, since the others are unused and not needed for decoding.
        createNumpressHelper (encodedByteArray |> Array.take (encodeInt + 5)) encodeInt array.Length

module NumpressDecodingHelpers =

    let decodePIC ((encodedByteArray, encInt, length): (byte[] * int * int)) =
        //empty array which gets filled by the decodePic function
        let decodedArray = Array.init length (fun i -> 0.)
        //decoding happens here
        let decode = Decode.decodePic (encodedByteArray, encInt , decodedArray)
        decodedArray

    let decodeLin ((encodedByteArray, enc, length): (byte[] * int * int)) =
        //empty array which gets filled by the decodeLinear function
        let decodedArray = Array.init (length) (fun i -> 0.)
        //decoding happens here
        let decode = Decode.decodeLinear (encodedByteArray, enc, decodedArray)
        decodedArray