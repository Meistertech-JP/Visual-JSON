' SPDX-License-Identifier: MPL-2.0
Imports System.IO
Imports System.Diagnostics
Imports System.Text
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports VisualJson.Core.Conversion
Imports VisualJson.Core.Infrastructure
Imports VisualJson.Core.Models
Imports VisualJson.Core.Parsing
Imports VisualJson.Core.Serialization
Imports VisualJson.Core.Services
Imports VisualJson.Core.Validation

<TestClass>
Public Class SchemaValidationTests

    <TestMethod(DisplayName:="UT-M2-001 schema required error")>
    Public Sub SchemaRequiredError()
        Dim schemaValidation = New SchemaValidationService()
        Dim schema = "{""type"":""object"",""required"":[""name""]}"
        Dim diagnostics = schemaValidation.Validate("{}", schema, "local-schema.json")

        AssertEqual(1, diagnostics.Count, "required diagnostic count")
        AssertEqual("SCH-REQUIRED", diagnostics(0).ErrorCode, "required code")
        AssertEqual("#/required", diagnostics(0).SchemaPath, "required schema path")
        AssertEqual("local-schema.json", diagnostics(0).SchemaUri, "schema uri")
    End Sub

    <TestMethod(DisplayName:="UT-M2-002 schema type error with body position")>
    Public Sub SchemaTypeErrorWithBodyPosition()
        Dim parser = New JsonParserService()
        Dim schemaValidation = New SchemaValidationService()
        Dim instance = "{" & Environment.NewLine & "  ""count"": ""abc""" & Environment.NewLine & "}"
        Dim schema = "{""type"":""object"",""properties"":{""count"":{""type"":""integer""}}}"
        Dim root = parser.Parse(instance).Root
        Dim diagnostics = schemaValidation.Validate(instance, schema, "", root)

        AssertEqual(1, diagnostics.Count, "type diagnostic count")
        AssertEqual("SCH-TYPE", diagnostics(0).ErrorCode, "type code")
        AssertEqual("/count", diagnostics(0).JsonPointer, "type pointer")
        AssertEqual("$.count", diagnostics(0).JsonPath, "type json path")
        AssertEqual(2, diagnostics(0).Line.Value, "type body line")
        AssertContains(diagnostics(0).SchemaPath, "#/properties/count/type", "type schema path")
    End Sub

    <TestMethod(DisplayName:="UT-M2-003 schema pattern error")>
    Public Sub SchemaPatternError()
        Dim schemaValidation = New SchemaValidationService()
        Dim schema = "{""type"":""object"",""properties"":{""code"":{""type"":""string"",""pattern"":""^[A-Z]+$""}}}"
        Dim diagnostics = schemaValidation.Validate("{""code"":""abc""}", schema)

        AssertEqual(1, diagnostics.Count, "pattern diagnostic count")
        AssertEqual("SCH-PATTERN", diagnostics(0).ErrorCode, "pattern code")
    End Sub

    <TestMethod(DisplayName:="Schema enum range additionalProperties and items")>
    Public Sub SchemaEnumRangeAdditionalAndItems()
        Dim schemaValidation = New SchemaValidationService()
        Dim schema = "{""type"":""object""," &
            """properties"":{" &
            """color"":{""enum"":[""red"",""blue""]}," &
            """age"":{""type"":""integer"",""minimum"":0,""maximum"":150}," &
            """tags"":{""type"":""array"",""items"":{""type"":""string""}}}," &
            """additionalProperties"":false}"
        Dim instance = "{""color"":""green"",""age"":200,""tags"":[""ok"",5],""extra"":1}"
        Dim diagnostics = schemaValidation.Validate(instance, schema)

        AssertTrue(diagnostics.Any(Function(item) item.ErrorCode = "SCH-ENUM"), "enum error")
        AssertTrue(diagnostics.Any(Function(item) item.ErrorCode = "SCH-MAXIMUM"), "maximum error")
        AssertTrue(diagnostics.Any(Function(item) item.ErrorCode = "SCH-TYPE" AndAlso item.JsonPointer = "/tags/1"), "items type error")
        AssertTrue(diagnostics.Any(Function(item) item.ErrorCode = "SCH-ADDITIONAL" AndAlso item.JsonPointer = "/extra"), "additional properties error")

        Dim belowMinimum = schemaValidation.Validate("{""age"":-1}", schema)
        AssertTrue(belowMinimum.Any(Function(item) item.ErrorCode = "SCH-MINIMUM"), "minimum error")
    End Sub

    <TestMethod(DisplayName:="IT-M2-001 schema diagnostics map to body location")>
    Public Sub SchemaDiagnosticsMapToBodyLocation()
        Dim parser = New JsonParserService()
        Dim schemaValidation = New SchemaValidationService()
        Dim instance = "{" & Environment.NewLine &
            "  ""user"": {" & Environment.NewLine &
            "    ""name"": 5" & Environment.NewLine &
            "  }" & Environment.NewLine &
            "}"
        Dim schema = "{""properties"":{""user"":{""properties"":{""name"":{""type"":""string""}}}}}"
        Dim root = parser.Parse(instance).Root
        Dim diagnostics = schemaValidation.Validate(instance, schema, "", root)

        AssertEqual(1, diagnostics.Count, "nested diagnostic count")
        AssertEqual("/user/name", diagnostics(0).JsonPointer, "nested pointer")
        AssertEqual(3, diagnostics(0).Line.Value, "nested body line for error jump")
        AssertTrue(diagnostics(0).Column.HasValue, "nested body column for error jump")
        AssertTrue(diagnostics(0).RelatedRange IsNot Nothing, "related range present")
    End Sub

    <TestMethod(DisplayName:="UT-P2-SCH-001 const mismatch reports SCH-CONST")>
    Public Sub P2SchemaConstMismatch()
        Dim validation = New SchemaValidationService()
        Dim schema = "{""properties"":{""env"":{""const"":""prod""}}}"

        Dim diagnostics = validation.Validate("{""env"":""dev""}", schema)
        AssertEqual(1, diagnostics.Count, "const diagnostic count")
        AssertEqual("SCH-CONST", diagnostics(0).ErrorCode, "const code")
        AssertEqual("/env", diagnostics(0).JsonPointer, "const pointer")

        AssertEqual(0, validation.Validate("{""env"":""prod""}", schema).Count, "matching const passes")
    End Sub

    <TestMethod(DisplayName:="UT-P2-SCH-002 string length violations report min and max codes")>
    Public Sub P2SchemaStringLength()
        Dim validation = New SchemaValidationService()
        Dim schema = "{""properties"":{""code"":{""minLength"":3,""maxLength"":5}}}"

        Dim tooShort = validation.Validate("{""code"":""ab""}", schema)
        AssertEqual("SCH-MINLENGTH", tooShort(0).ErrorCode, "min length code")

        Dim tooLong = validation.Validate("{""code"":""abcdef""}", schema)
        AssertEqual("SCH-MAXLENGTH", tooLong(0).ErrorCode, "max length code")

        AssertEqual(0, validation.Validate("{""code"":""abcd""}", schema).Count, "in-range string passes")
        AssertEqual(0, validation.Validate("{""code"":""𠀋𠀋𠀋""}", schema).Count, "surrogate pairs count as single code points")
    End Sub

    <TestMethod(DisplayName:="UT-P2-SCH-003 local ref delegates to definitions")>
    Public Sub P2SchemaLocalRef()
        Dim validation = New SchemaValidationService()
        Dim schema = "{""properties"":{""user"":{""$ref"":""#/definitions/person""}},""definitions"":{""person"":{""type"":""object"",""required"":[""name""]}}}"

        Dim diagnostics = validation.Validate("{""user"":{}}", schema)
        AssertEqual(1, diagnostics.Count, "ref diagnostic count")
        AssertEqual("SCH-REQUIRED", diagnostics(0).ErrorCode, "referenced rule applied")
        AssertEqual("/user", diagnostics(0).JsonPointer, "referenced rule pointer")

        AssertEqual(0, validation.Validate("{""user"":{""name"":""a""}}", schema).Count, "valid instance passes through ref")

        Dim missingRef = validation.Validate("{""user"":{}}", "{""properties"":{""user"":{""$ref"":""#/definitions/absent""}}}")
        AssertEqual("SCH-REF-NOTFOUND", missingRef(0).ErrorCode, "unresolved ref code")
    End Sub

    <TestMethod(DisplayName:="UT-P2-SCH-004 ref cycle stops without recursion")>
    Public Sub P2SchemaRefCycle()
        Dim validation = New SchemaValidationService()
        Dim schema = "{""$ref"":""#/definitions/a"",""definitions"":{""a"":{""$ref"":""#/definitions/b""},""b"":{""$ref"":""#/definitions/a""}}}"

        Dim timer = Stopwatch.StartNew()
        Dim diagnostics = validation.Validate("{}", schema)
        timer.Stop()

        AssertEqual(1, diagnostics.Count, "cycle reported once")
        AssertEqual("SCH-REF-CYCLE", diagnostics(0).ErrorCode, "cycle code")
        AssertTrue(timer.Elapsed < TimeSpan.FromSeconds(1), "cycle detection terminates quickly")
    End Sub

    <TestMethod(DisplayName:="UT-P2-SCH-005 external ref warns without network access")>
    Public Sub P2SchemaExternalRefWarning()
        Dim validation = New SchemaValidationService()
        Dim diagnostics = validation.Validate("{}", "{""$ref"":""https://example.com/schema.json""}")

        AssertEqual(1, diagnostics.Count, "external ref diagnostic count")
        AssertEqual("SCH-REF-UNSUPPORTED", diagnostics(0).ErrorCode, "external ref code")
        AssertEqual("Warning", diagnostics(0).Severity, "external ref severity")
    End Sub

    <TestMethod(DisplayName:="UT-P2-SCH-006 format violations warn")>
    Public Sub P2SchemaFormatWarnings()
        Dim validation = New SchemaValidationService()
        Dim schema = "{""properties"":{""at"":{""format"":""date-time""},""mail"":{""format"":""email""},""link"":{""format"":""uri""},""other"":{""format"":""custom-unknown""}}}"

        Dim invalid = validation.Validate("{""at"":""not-a-date"",""mail"":""nope"",""link"":""not a uri"",""other"":""anything""}", schema)
        AssertEqual(3, invalid.Count, "three format warnings")
        AssertTrue(invalid.All(Function(item) item.ErrorCode = "SCH-FORMAT" AndAlso item.Severity = "Warning"), "format warnings only")

        Dim valid = validation.Validate("{""at"":""2026-07-09T12:00:00Z"",""mail"":""a@b.co"",""link"":""https://example.com/x""}", schema)
        AssertEqual(0, valid.Count, "valid formats pass")
    End Sub
End Class
