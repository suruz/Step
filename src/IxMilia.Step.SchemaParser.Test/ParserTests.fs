﻿// Copyright (c) IxMilia.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

module ParserTests

open System.Linq
open IxMilia.Step.SchemaParser
open FParsec
open Xunit

let parse str =
    match run SchemaParser.parser str with
    | Success(result, _, _) -> result
    | Failure(errorMessage, _, _) -> failwith errorMessage

let arrayEqual (a : 'a array, b : 'a array) =
    Assert.Equal(a.Length, b.Length)
    Array.zip a b
    |> Array.map (fun (l, r) -> l = r)
    |> Array.fold (&&) true
    |> Assert.True

[<Fact>]
let ``empty schema``() =
    let schema = parse " SCHEMA test_schema1 ; END_SCHEMA ; "
    Assert.Equal("test_schema1", schema.Id)

[<Fact>]
let ``schema with version``() =
    let schema = parse " SCHEMA name \"version with \"\" double quote\" ; END_SCHEMA ; "
    Assert.Equal("version with \" double quote", schema.Version)

(*
[<Fact>]
let ``empty entity``() =
    let schema = parse " SCHEMA test_schema ; ENTITY empty_entity ; END_ENTITY ; END_SCHEMA ; "
    Assert.Equal("empty_entity", schema.Entities.Single().Name)

[<Fact>]
let ``simple entity``() =
    let schema = parse " SCHEMA s ; ENTITY point ; x : REAL ; y : REAL ; END_ENTITY ; END_SCHEMA ; "
    let entity = schema.Entities.Single()
    Assert.Equal(2, entity.Properties.Length)
    Assert.Equal("x", entity.Properties.[0].Name)
    Assert.Equal("REAL", entity.Properties.[0].Type.TypeName)
    Assert.Equal("y", entity.Properties.[1].Name)
    Assert.Equal("REAL", entity.Properties.[1].Type.TypeName)

[<Fact>]
let ``entity with optional parameter``() =
    let schema = parse " SCHEMA s ; ENTITY point ; x : REAL ; y : OPTIONAL REAL ; END_ENTITY ; END_SCHEMA ; "
    let entity = schema.Entities.Single()
    Assert.Equal(2, entity.Properties.Length)
    Assert.Equal("REAL", entity.Properties.[0].Type.TypeName)
    Assert.Equal(None, entity.Properties.[0].Type.Bounds)
    Assert.False(entity.Properties.[0].Type.IsOptional)
    Assert.Equal("REAL", entity.Properties.[1].Type.TypeName)
    Assert.True(entity.Properties.[1].Type.IsOptional)
    Assert.Equal(None, entity.Properties.[1].Type.Bounds)

[<Fact>]
let ``entity property with non-built-in-type``() =
    let schema = parse " SCHEMA s ; ENTITY circle ; center : point ; END_ENTITY ; END_SCHEMA ; "
    Assert.Equal("point", schema.Entities.Single().Properties.Single().Type.TypeName)

[<Fact>]
let ``entity with derived``() =
    let schema = parse " SCHEMA s ; ENTITY square ; size : REAL ; DERIVE area : REAL := size * size ; END_ENTITY ; END_SCHEMA ; "
    Assert.Equal("area", schema.Entities.Single().DerivedProperties.Single().Property.Name)
    Assert.Equal(Mul(Identifier("size"), Identifier("size")), schema.Entities.Single().DerivedProperties.Single().Expression)

[<Fact>]
let ``multiple entities``() =
    let schema = parse " SCHEMA s ; ENTITY a ; END_ENTITY ; ENTITY b ; END_ENTITY ; END_SCHEMA ; "
    Assert.Equal(2, schema.Entities.Length)
    Assert.Equal("a", schema.Entities.First().Name)
    Assert.Equal("b", schema.Entities.Last().Name)
//*)

[<Fact>]
let ``comments``() =
    let schema = parse @"
-- single line comment
SCHEMA s (* multi-line comment
*) ;
-- comment 1
-- comment 2
-- comment 3

(* another multi-line comment *)

END_SCHEMA ;
"
    Assert.Equal("s", schema.Id)

(*
[<Fact>]
let ``simple types``() =
    let schema = parse " SCHEMA s ; TYPE length = REAL ; END_TYPE ; TYPE width = REAL ; END_TYPE ; END_SCHEMA ; "
    Assert.Equal(2, schema.Types.Length)
    Assert.Equal("length", schema.Types.First().Name)
    Assert.Equal("REAL", schema.Types.First().Types.Single())
    Assert.Equal("width", schema.Types.Last().Name)
    Assert.Equal("REAL", schema.Types.Last().Types.Single())

[<Fact>]
let ``type and entity``() =
    let schema = parse " SCHEMA s ; TYPE double = REAL ; END_TYPE ; ENTITY point ; END_ENTITY ; END_SCHEMA ; "
    Assert.Equal("double", schema.Types.Single().Name)
    Assert.Equal("point", schema.Entities.Single().Name)

[<Fact>]
let ``type with empty select values``() =
    let schema = parse " SCHEMA s ; TYPE foo = SELECT ( ) ; END_TYPE ; END_SCHEMA ; "
    Assert.Equal(0, schema.Types.Single().Types.Length)

[<Fact>]
let ``type with one select value``() =
    let schema = parse " SCHEMA s ; TYPE foo = SELECT ( bar ) ; END_TYPE ; END_SCHEMA ; "
    Assert.Equal("bar", schema.Types.Single().Types.Single())

[<Fact>]
let ``type with two select values``() =
    let schema = parse " SCHEMA s ; TYPE foo = SELECT ( bar , baz ) ; END_TYPE ; END_SCHEMA ; "
    Assert.Equal(2, schema.Types.Single().Types.Length)
    Assert.Equal("bar", schema.Types.Single().Types.First())
    Assert.Equal("baz", schema.Types.Single().Types.Last())

[<Fact>]
let ``enumeration type``() =
    let schema = parse " SCHEMA s ; TYPE numbers = ENUMERATION OF ( uno , dos , tres ) ; END_TYPE ; END_SCHEMA ; "
    arrayEqual([|"uno"; "dos"; "tres"|], schema.Types.Single().Values)

[<Fact>]
let ``type with restriction``() =
    let schema = parse " SCHEMA s ; TYPE measure = REAL ; WHERE wr1 : SELF >= 0 ; END_TYPE ; END_SCHEMA ; "
    Assert.Equal(TypeRestriction("wr1", GreaterEqual(Identifier("SELF"), Number(0.0))), schema.Types.Single().Restrictions.Single())

[<Fact>]
let ``type with multiple restrictions``() =
    let schema = parse " SCHEMA s ; TYPE measure = REAL ; WHERE wr1 : SELF >= 0 ; wr2 : (SELF > 0) AND (SELF < 10) ; END_TYPE ; END_SCHEMA ; "
    Assert.Equal(2, schema.Types.Single().Restrictions.Length)
    Assert.Equal(TypeRestriction("wr1", GreaterEqual(Identifier("SELF"), Number(0.0))), schema.Types.Single().Restrictions.First())
    Assert.Equal(TypeRestriction("wr2", And(Greater(Identifier("SELF"), Number(0.0)), Less(Identifier("SELF"), Number(10.0)))), schema.Types.Single().Restrictions.Last())

[<Fact>]
let ``type with function restriction``() =
    let schema = parse " SCHEMA s ; TYPE measure = REAL ; WHERE wr1 : EXISTS ( SELF ) OR FOO ( 1.2, 3.4 ) ; END_TYPE ; END_SCHEMA ; "
    Assert.Equal(TypeRestriction("wr1", Or(Function("EXISTS", [| Identifier("SELF") |]), Function("FOO", [| Number(1.2); Number(3.4) |]))), schema.Types.Single().Restrictions.Single())

[<Fact>]
let ``expression with an index``() =
    let schema = parse " SCHEMA s ; ENTITY e ; WHERE wr1 : foo[bar] > 0 ; END_ENTITY ; END_SCHEMA ; "
    Assert.Equal(Greater(Index("foo", [| Identifier("bar") |]), Number(0.0)), schema.Entities.Single().Restrictions.Single().Expression)

[<Fact>]
let ``entity with restriction``() =
    let schema = parse " SCHEMA s ; ENTITY person ; name : STRING ; alias : STRING ; WHERE wr1 : EXISTS ( name ) OR EXISTS ( alias ) ; END_ENTITY ; END_SCHEMA ; "
    Assert.Equal(TypeRestriction("wr1", Or(Function("EXISTS", [| Identifier("name") |]), Function("EXISTS", [| Identifier("alias") |]))), schema.Entities.Single().Restrictions.Single())

[<Fact>]
let ``entity with complex restriction``() =
    let schema = parse "SCHEMA s ; ENTITY e ; WHERE wr1 : 'asdf.jkl' IN TYPEOF ( SELF\\foo.bar ) ; END_ENTITY ; END_SCHEMA ; "
    Assert.Equal(In(String("asdf.jkl"), Function("TYPEOF", [| Identifier("SELF\\foo.bar") |])), schema.Entities.Single().Restrictions.Single().Expression)

[<Fact>]
let ``entity with no subtype or supertype``() =
    let schema = parse " SCHEMA s ; ENTITY person ; name : STRING ; END_ENTITY ; END_SCHEMA ; "
    Assert.Equal("", schema.Entities.Single().SubType)
    Assert.Empty(schema.Entities.Single().SuperTypes)

[<Fact>]
let ``entity with subtype``() =
    let schema = parse " SCHEMA s ; ENTITY person SUBTYPE OF ( mammal ) ; name : STRING ; END_ENTITY ; END_SCHEMA ; "
    Assert.Equal("mammal", schema.Entities.Single().SubType)

[<Fact>]
let ``entity with single supertype``() =
    let schema = parse " SCHEMA s ; ENTITY mammal SUPERTYPE OF ( animal ) ; END_ENTITY ; END_SCHEMA ; "
    Assert.Equal("animal", schema.Entities.Single().SuperTypes.Single())

[<Fact>]
let ``entity with many supertypes``() =
    let schema = parse " SCHEMA s ; ENTITY mammal SUPERTYPE OF ( ONEOF ( animal , not_animal ) ) ; END_ENTITY ; END_SCHEMA ; "
    arrayEqual([| "animal"; "not_animal" |], schema.Entities.Single().SuperTypes)

[<Fact>]
let ``entity property with upper/lower bounds``() =
    let schema = parse " SCHEMA s ; ENTITY e ; p : SET [ 2 : ? ] OF REAL ; END_ENTITY ; END_SCHEMA ; "
    let propertyType = schema.Entities.Single().Properties.Single().Type
    Assert.False(propertyType.IsOptional)
    Assert.Equal("REAL", propertyType.TypeName)
    Assert.Equal(Some(Some(2), None), propertyType.Bounds)

[<Fact>]
let ``expression with multi-line qualified identifier``() =
    let schema = parse " SCHEMA s ; ENTITY e ; WHERE wr1 : foo\\\nbar.\nbaz > 0 ; END_ENTITY ; END_SCHEMA ; "
    Assert.Equal(Greater(Identifier("foo\\bar.baz"), Number(0.0)), schema.Entities.Single().Restrictions.Single().Expression)

[<Fact>]
let ``expression with an array``() =
    let schema = parse " SCHEMA s ; ENTITY e ; WHERE wr1 : [ 'a' , 'b' ] ; END_ENTITY ; END_SCHEMA ; "
    Assert.Equal(Array([|String("a"); String("b")|]), schema.Entities.Single().Restrictions.Single().Expression)
// *)
