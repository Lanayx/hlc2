module HCup.BufferSerializers

open System
open System.Buffers
open System.Globalization
open System.IO
open System.Text

open FSharp.NativeInterop

open HCup.Models
open HCup.Helpers
open HCup

#nowarn "9"

open System.Collections
open Giraffe

let private utf8Encoding = Encoding.UTF8
let utf8 : string -> byte[] = utf8Encoding.GetBytes
let inline private writeArray (output : MemoryStream) array =
    output.Write(array, 0, array.Length)
let inline private stream buffer =
    new MemoryStream(buffer, 0, buffer.Length, true, true)

let inline private writeInt32 (output : MemoryStream) (number: int) =
    let numbersCount =
        if number >= 1_000_000_000 then 10
        elif number >= 100_000_000 then 9
        elif number >= 10_000_000 then 8
        elif number >= 1_000_000 then 7
        elif number >= 100_000 then 6
        elif number >= 10_000 then 5
        elif number >= 1_000 then 4
        elif number >= 100 then 3
        elif number >= 10 then 2
        else 1
    let buffer = NativePtr.stackalloc<byte> numbersCount
    let mutable num = number
    let loopMax = numbersCount - 1
    for i = loopMax downto 0 do
        NativePtr.set buffer i (byte (num % 10 + 48))
        num <- num / 10
    for i = 0 to loopMax do
        output.WriteByte (NativePtr.get buffer i)

let inline private writeInt32x (output : MemoryStream) (number: int32) =
    if number > 0
    then
        let numbersCount =
            if number >= 1_000_000_000 then 10
            elif number >= 100_000_000 then 9
            elif number >= 10_000_000 then 8
            elif number >= 1_000_000 then 7
            elif number >= 100_000 then 6
            elif number >= 10_000 then 5
            elif number >= 1_000 then 4
            elif number >= 100 then 3
            elif number >= 10 then 2
            else 1
        let buffer = NativePtr.stackalloc<byte> numbersCount
        let mutable num = number
        let loopMax = numbersCount - 1
        for i = loopMax downto 0 do
            NativePtr.set buffer i (byte (num % 10 + 48))
            num <- num / 10
        for i = 0 to loopMax do
            output.WriteByte (NativePtr.get buffer i)

    else
        let posNumber = -number
        let numbersCount =
            if posNumber >= 1_000_000_000 then 10
            elif posNumber >= 100_000_000 then 9
            elif posNumber >= 10_000_000 then 8
            elif posNumber >= 1_000_000 then 7
            elif posNumber >= 100_000 then 6
            elif posNumber >= 10_000 then 5
            elif posNumber >= 1_000 then 4
            elif posNumber >= 100 then 3
            elif posNumber >= 10 then 2
            else 1
        let buffer = NativePtr.stackalloc<byte> numbersCount
        let mutable num = posNumber
        let loopMax = numbersCount - 1
        for i = loopMax downto 0 do
            NativePtr.set buffer i (byte (num % 10 + 48))
            num <- num / 10
        output.WriteByte(byte '-')
        for i = 0 to loopMax do
            output.WriteByte (NativePtr.get buffer i)

let inline private writeString (output : MemoryStream) (str: string) =
    let buffer = ArrayPool.Shared.Rent (str.Length*2)
    let written = utf8Encoding.GetBytes(str,0,str.Length, buffer,0)
    output.Write(buffer, 0, written)
    ArrayPool.Shared.Return buffer

let inline private writeChar (output : MemoryStream) (chr: char) =
    output.WriteByte((byte)chr)

let private writeFloat (output : MemoryStream) (value: float) =
     let zeroByte = (byte)'0';
     if value = 0.0
     then
        output.WriteByte(zeroByte);output.WriteByte((byte)'.');output.WriteByte(zeroByte);
     else
         let intValue = (int)value
         writeInt32 output intValue
         output.WriteByte((byte)'.')
         let mutable decimalValue = (int)(Math.Round(value*100000.0, MidpointRounding.AwayFromZero)) - intValue*100000
         if decimalValue = 0
         then output.WriteByte(zeroByte)
         else
             let mutable temp = decimalValue
             while temp < 10000 do
                 temp <- temp*10
                 output.WriteByte(zeroByte)
             while decimalValue % 10 = 0 do
                 decimalValue <- decimalValue / 10
             writeInt32 output decimalValue

let fieldsMap =
    dict [
        "sex_eq", AccountField.Sex
        "email_domain", AccountField.Email
        "email_lt", AccountField.Email
        "email_gt", AccountField.Email
        "status_eq", AccountField.Status
        "status_neq", AccountField.Status
        "fname_eq", AccountField.Firstname
        "fname_any", AccountField.Firstname
        "fname_null", AccountField.Firstname
        "sname_eq", AccountField.Surname
        "sname_starts", AccountField.Surname
        "sname_null", AccountField.Surname
        "phone_code", AccountField.Phone
        "phone_null", AccountField.Phone
        "country_eq", AccountField.Country
        "country_null", AccountField.Country
        "city_eq", AccountField.City
        "city_any", AccountField.City
        "city_null", AccountField.City
        "birth_lt", AccountField.Birth
        "birth_gt", AccountField.Birth
        "birth_year", AccountField.Birth
        "interests_contains", AccountField.Interests
        "interests_any", AccountField.Interests
        "likes_contains", AccountField.Likes
        "premium_now", AccountField.Premium
        "premium_null", AccountField.Premium
    ]

let private ``{"accounts":[`` = utf8 "{\"accounts\":["
let private ``},{`` = utf8 "},{"
let private ``]}`` = utf8 "]}"

let private ``"email":"`` = utf8 "\"email\":\""
let private ``","id":`` = utf8 "\",\"id\":"


let private ``,"fname":"`` = utf8 ",\"fname\":\""
let private ``,"sname":"`` = utf8 ",\"sname\":\""
let private ``,"interests":[`` = utf8 ",\"interests\":["
let private ``,"status":"`` = utf8 ",\"status\":\""
let private ``,"premium":{`` = utf8 ",\"premium\":{"
let private ``"start":`` = utf8 "\"start\":"
let private ``,"finish":`` = utf8 ",\"finish\":"
let private ``,"sex":"`` = utf8 ",\"sex\":\""
let private ``,"phone":"`` = utf8 ",\"phone\":\""
let private ``,"likes":[`` = utf8 ",\"likes\":["
let private ``{"ts":`` = utf8 "{\"ts\":"
let private ``,"id":`` = utf8 ",\"id\":"
let private ``,"birth":`` = utf8 ",\"birth\":"
let private ``,"city":"`` = utf8 ",\"city\":\""
let private ``,"country":"`` = utf8 ",\"country\":\""


let writeField (field_predicate: string, acc: Account, output: MemoryStream) =
    let fieldType = fieldsMap.[field_predicate]
    match fieldType with
    | AccountField.Email -> ()
    | AccountField.Firstname ->
        if acc.fname |> isNotNull
        then
            writeArray output ``,"fname":"``
            writeString output acc.fname
            writeChar output '"'
    | AccountField.Surname ->
        if acc.sname |> isNotNull
        then
            writeArray output ``,"sname":"``
            writeString output acc.sname
            writeChar output '"'
    | AccountField.Interests ->
        if acc.interests |> isNotNull
        then
            writeArray output ``,"interests":[``
            let mutable interestCounter = 0
            for interest in acc.interests do
                writeChar output '"'
                writeString output interest
                writeChar output '"'
                interestCounter <- interestCounter + 1
                if (interestCounter < acc.interests.Length)
                then writeChar output ','
            writeChar output ']'
    | AccountField.Status ->
        if acc.status |> isNotNull
        then
            writeArray output ``,"status":"``
            writeString output acc.status
            writeChar output '"'
    | AccountField.Premium ->
        if (box acc.premium) |> isNotNull
        then
            writeArray output ``,"premium":{``
            writeArray output ``"start":``
            writeInt32 output acc.premium.start
            writeArray output ``,"finish":``
            writeInt32 output acc.premium.finish
            writeChar output '}'
    | AccountField.Sex ->
        writeArray output ``,"sex":"``
        writeChar output acc.sex
        writeChar output '"'
    | AccountField.Phone ->
        if acc.phone |> isNotNull
        then
            writeArray output ``,"phone":"``
            writeString output acc.phone
            writeChar output '"'
    | AccountField.Likes ->
        if acc.likes |> isNotNull
        then
            writeArray output ``,"likes":[``
            let mutable likesCounter = 0
            for like in acc.likes do
                writeArray output ``{"ts":``
                writeInt32 output like.ts
                writeArray output ``,"id":``
                writeInt32 output like.id
                writeChar output '}'
                likesCounter <- likesCounter + 1
                if (likesCounter < acc.likes.Length)
                then writeChar output ','
            writeChar output ']'
    | AccountField.Birth ->
        writeArray output ``,"birth":``
        writeInt32x output acc.birth
    | AccountField.City ->
        if acc.city |> isNotNull
        then
            writeArray output ``,"city":"``
            writeString output acc.city
            writeChar output '"'
    | AccountField.Country ->
        if acc.country |> isNotNull
        then
            writeArray output ``,"country":"``
            writeString output acc.country
            writeChar output '"'
    | _ -> ()

let getAccsSize (accs: Account[], field_predicates: string[]) =
    let mutable baseSize = 30 * accs.Length * (2+field_predicates.Length)
    for field in field_predicates do       
        if field =~ "likes_contains"
        then
            let likesCount = accs
                            |> Array.fold (fun sum acc -> sum + acc.likes.Length ) 0
            baseSize <- baseSize + likesCount * 30
        if field =~ "interests_contains" || field =~ "interests_any"
        then
            let likesCount = accs
                            |> Array.fold (fun sum acc -> sum + acc.interests.Length ) 0
            baseSize <- baseSize + likesCount * 20
    baseSize

let serializeAccounts (accs: Account[], field_predicates: string[]): MemoryStream =
    let array = ArrayPool.Shared.Rent (50 + getAccsSize(accs, field_predicates))
    let output = stream array
    writeArray output ``{"accounts":[``
    let mutable start = true
    for acc in accs do
        if start
        then
            start <- false
            writeChar output '{'
        else
            writeArray output ``},{``

        writeArray output ``"email":"``
        writeString output acc.email
        writeArray output ``","id":``
        writeInt32 output acc.id

        for field_predicate in field_predicates do
            writeField(field_predicate, acc, output)
    if start |> not
    then writeChar output '}'
    writeArray output ``]}``
    output