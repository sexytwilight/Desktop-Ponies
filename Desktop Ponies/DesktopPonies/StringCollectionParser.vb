﻿Imports System.Globalization
Imports System.IO
Imports System.Runtime.CompilerServices

Public Class StringCollectionParser
    Private Const NotParsed = "This value was not parsed due to an earlier parsing failure."
    Private Const NullFailure = "This value is missing."
    Private Const BooleanFailureFormat = "Could not interpret '{0}' as a Boolean value. Valid values are '{1}' or '{2}'."
    Private Const IntegerFailureFormat = "Could not interpret '{0}' as an integer value."
    Private Const FloatFailureFormat = "Could not interpret '{0}' as a decimal value."
    Private Const MapFailureFormat = "'{0}' is not valid. Valid values are: {1}."
    Private Const ProjectFailureFormat = "'{0}' is not valid. Error: {1}"
    Private Const Vector2FailureFormat =
        "Could not interpret '{0}' as a point. Points consist of two integers separated by a comma, e.g. '0,0'."
    Private Const PathNullFailureFormat = "The specified path was null."
    Private Const PathInvalidCharsFailureFormat = "This path contains invalid characters: {0}"
    Private Const PathRootedFailureFormat = "A relative path must be used, an absolute path is not allowed: {0}"
    Private Const FileNotFoundFailureFormat = "This file does not exist, or cannot be accessed: {0}"
    Private Const OutOfRangeFailureFormat = "Value of {0} is out of range. Value must be between {1} and {2} (inclusive)."

    Private ReadOnly _issues As New List(Of ParseIssue)
    Public ReadOnly Property Issues As ReadOnlyList(Of ParseIssue)
        Get
            Return New ReadOnlyList(Of ParseIssue)(_issues)
        End Get
    End Property
    Private items As String()
    Private itemNames As String()
    Private index As Integer
    Private _lastParseFailed As Boolean
    Protected ReadOnly Property LastParseFailed As Boolean
        Get
            Return _lastParseFailed
        End Get
    End Property
    Private _result As ParseResult
    Public ReadOnly Property Result As ParseResult
        Get
            Return _result
        End Get
    End Property
    Public Sub New(itemsToParse As String(), itemNames As String())
        items = Argument.EnsureNotNull(itemsToParse, "itemsToParse")
        Me.itemNames = If(itemNames, {})
    End Sub
    Protected Function GetNextItem() As String
        Dim item As String = Nothing
        If index < items.Length Then item = items(index)
        index += 1
        Return item
    End Function
    Protected Function HandleParsed(Of T)(parsed As Parsed(Of T)) As T
        _lastParseFailed = parsed.Result = ParseResult.Failed
        Dim lastResult = parsed.Result
        Dim elementDefaultUsed = parsed.Result = ParseResult.Fallback AndAlso parsed.Source Is Nothing
        If elementDefaultUsed Then lastResult = ParseResult.Success
        _result = _result.Combine(lastResult)
        If parsed.Result <> ParseResult.Success AndAlso Not elementDefaultUsed Then
            Dim i = index - 1
            _issues.Add(New ParseIssue(i,
                                       If(i < itemNames.Length, itemNames(i), Nothing),
                                       parsed.Source,
                                       If(LastParseFailed, Nothing, parsed.Value.ToString()),
                                       parsed.Reason))
        End If
        Return parsed.Value
    End Function
    Public Function NoParse() As String
        Return HandleParsed(Parsed.Success(GetNextItem()))
    End Function
    Public Function NotNull() As String
        Return NotNull(Nothing)
    End Function
    Public Function NotNull(fallback As String) As String
        Return HandleParsed(ParsedNotNull(GetNextItem(), fallback))
    End Function
    Private Shared Function ParsedNotNull(s As String, fallback As String) As Parsed(Of String)
        If s IsNot Nothing Then
            Return Parsed.Success(s)
        ElseIf fallback IsNot Nothing Then
            Return Parsed.Fallback(s, fallback, NullFailure)
        Else
            Return Parsed.Failed(Of String)(s, NullFailure)
        End If
    End Function
    Public Function NotNullOrEmpty() As String
        Return NotNullOrEmpty(Nothing)
    End Function
    Public Function NotNullOrEmpty(fallback As String) As String
        Return HandleParsed(ParsedNotNullOrEmpty(GetNextItem(), fallback))
    End Function
    Private Shared Function ParsedNotNullOrEmpty(s As String, fallback As String) As Parsed(Of String)
        If Not String.IsNullOrEmpty(s) Then
            Return Parsed.Success(s)
        ElseIf fallback IsNot Nothing Then
            Return Parsed.Fallback(s, fallback, NullFailure)
        Else
            Return Parsed.Failed(Of String)(s, NullFailure)
        End If
    End Function
    Public Function NotNullOrWhiteSpace() As String
        Return NotNullOrWhiteSpace(Nothing)
    End Function
    Public Function NotNullOrWhiteSpace(fallback As String) As String
        Return HandleParsed(ParsedNotNullOrWhiteSpace(GetNextItem(), fallback))
    End Function
    Private Shared Function ParsedNotNullOrWhiteSpace(s As String, fallback As String) As Parsed(Of String)
        If Not String.IsNullOrWhiteSpace(s) Then
            Return Parsed.Success(s)
        ElseIf fallback IsNot Nothing Then
            Return Parsed.Fallback(s, fallback, NullFailure)
        Else
            Return Parsed.Failed(Of String)(s, NullFailure)
        End If
    End Function
    Public Function ParseBoolean() As Boolean
        Return ParseBoolean(Nothing)
    End Function
    Public Function ParseBoolean(fallback As Boolean?) As Boolean
        Return HandleParsed(ParsedBoolean(GetNextItem(), fallback))
    End Function
    Private Shared Function ParsedBoolean(s As String, fallback As Boolean?) As Parsed(Of Boolean)
        Dim result As Boolean
        If Boolean.TryParse(s, result) Then
            Return Parsed.Success(result)
        ElseIf fallback IsNot Nothing Then
            Return Parsed.Fallback(s, fallback.Value, BooleanFailureFormat.FormatWith(s, Boolean.TrueString, Boolean.FalseString))
        Else
            Return Parsed.Failed(Of Boolean)(s, BooleanFailureFormat.FormatWith(s, Boolean.TrueString, Boolean.FalseString))
        End If
    End Function
    Public Function ParseInt32() As Integer
        Return ParseInt32(Integer.MinValue, Integer.MaxValue)
    End Function
    Public Function ParseInt32(fallback As Integer) As Integer
        Return ParseInt32(fallback, Integer.MinValue, Integer.MaxValue)
    End Function
    Public Function ParseInt32(min As Integer, max As Integer) As Integer
        Return ParseInt32Internal(Nothing, min, max)
    End Function
    Public Function ParseInt32(fallback As Integer, min As Integer, max As Integer) As Integer
        Return ParseInt32Internal(fallback, min, max)
    End Function
    Private Function ParseInt32Internal(fallback As Integer?, min As Integer, max As Integer) As Integer
        Return HandleParsed(ParsedInt32(GetNextItem(), fallback, min, max))
    End Function
    Private Shared Function ParsedInt32(s As String, fallback As Integer?, Optional min As Integer = Integer.MinValue, Optional max As Integer = Integer.MaxValue) As Parsed(Of Integer)
        If min > max Then Throw New ArgumentException("min must be less than or equal to max.")
        Dim failReason As String = Nothing
        Dim result As Integer
        If Not Number.TryParseInt32Invariant(s, result) Then
            failReason = IntegerFailureFormat.FormatWith(s)
        End If
        If failReason Is Nothing AndAlso (result < min OrElse result > max) Then
            failReason = OutOfRangeFailureFormat.FormatWith(result, min, max)
        End If
        If failReason Is Nothing Then
            Return Parsed.Success(result)
        ElseIf fallback IsNot Nothing Then
            Return Parsed.Fallback(s, fallback.Value, failReason)
        Else
            Return Parsed.Failed(Of Integer)(s, failReason)
        End If
    End Function
    Public Function ParseSingle() As Single
        Return ParseSingle(Single.MinValue, Single.MaxValue)
    End Function
    Public Function ParseSingle(fallback As Single) As Single
        Return ParseSingle(fallback, Single.MinValue, Single.MaxValue)
    End Function
    Public Function ParseSingle(min As Single, max As Single) As Single
        Return ParseSingleInternal(Nothing, min, max)
    End Function
    Public Function ParseSingle(fallback As Single, min As Single, max As Single) As Single
        Return ParseSingleInternal(fallback, min, max)
    End Function
    Private Function ParseSingleInternal(fallback As Single?, min As Single, max As Single) As Single
        Return HandleParsed(ParsedSingle(GetNextItem(), fallback, min, max))
    End Function
    Private Shared Function ParsedSingle(s As String, fallback As Single?, min As Single, max As Single) As Parsed(Of Single)
        If min > max Then Throw New ArgumentException("min must be less than or equal to max.")
        Dim failReason As String = Nothing
        Dim result As Single
        If Not Number.TryParseSingleInvariant(s, result) Then
            failReason = FloatFailureFormat.FormatWith(s)
        End If
        If failReason Is Nothing AndAlso (result < min OrElse result > max) Then
            failReason = OutOfRangeFailureFormat.FormatWith(result, min, max)
        End If
        If failReason Is Nothing Then
            Return Parsed.Success(result)
        ElseIf fallback IsNot Nothing Then
            Return Parsed.Fallback(s, fallback.Value, failReason)
        Else
            Return Parsed.Failed(Of Single)(s, failReason)
        End If
    End Function
    Public Function ParseDouble() As Double
        Return ParseDouble(Double.MinValue, Double.MaxValue)
    End Function
    Public Function ParseDouble(fallback As Double) As Double
        Return ParseDouble(fallback, Double.MinValue, Double.MaxValue)
    End Function
    Public Function ParseDouble(min As Double, max As Double) As Double
        Return ParseDoubleInternal(Nothing, min, max)
    End Function
    Public Function ParseDouble(fallback As Double, min As Double, max As Double) As Double
        Return ParseDoubleInternal(fallback, min, max)
    End Function
    Private Function ParseDoubleInternal(fallback As Double?, min As Double, max As Double) As Double
        Return HandleParsed(ParsedDouble(GetNextItem(), fallback, min, max))
    End Function
    Private Shared Function ParsedDouble(s As String, fallback As Double?, min As Double, max As Double) As Parsed(Of Double)
        If min > max Then Throw New ArgumentException("min must be less than or equal to max.")
        Dim failReason As String = Nothing
        Dim result As Double
        If Not Number.TryParseDoubleInvariant(s, result) Then
            failReason = FloatFailureFormat.FormatWith(s)
        End If
        If failReason Is Nothing AndAlso (result < min OrElse result > max) Then
            failReason = OutOfRangeFailureFormat.FormatWith(result, min, max)
        End If
        If failReason Is Nothing Then
            Return Parsed.Success(result)
        ElseIf fallback IsNot Nothing Then
            Return Parsed.Fallback(s, fallback.Value, failReason)
        Else
            Return Parsed.Failed(Of Double)(s, failReason)
        End If
    End Function
    Public Function ParseDecimal() As Decimal
        Return ParseDecimal(Decimal.MinValue, Decimal.MaxValue)
    End Function
    Public Function ParseDecimal(fallback As Decimal) As Decimal
        Return ParseDecimal(fallback, Decimal.MinValue, Decimal.MaxValue)
    End Function
    Public Function ParseDecimal(min As Decimal, max As Decimal) As Decimal
        Return ParseDecimalInternal(Nothing, min, max)
    End Function
    Public Function ParseDecimal(fallback As Decimal, min As Decimal, max As Decimal) As Decimal
        Return ParseDecimalInternal(fallback, min, max)
    End Function
    Private Function ParseDecimalInternal(fallback As Decimal?, min As Decimal, max As Decimal) As Decimal
        Return HandleParsed(ParsedDecimal(GetNextItem(), fallback, min, max))
    End Function
    Private Shared Function ParsedDecimal(s As String, fallback As Decimal?, min As Decimal, max As Decimal) As Parsed(Of Decimal)
        If min > max Then Throw New ArgumentException("min must be less than or equal to max.")
        Dim failReason As String = Nothing
        Dim result As Decimal
        If Not Number.TryParseDecimalInvariant(s, result) Then
            failReason = FloatFailureFormat.FormatWith(s)
        End If
        If failReason Is Nothing AndAlso (result < min OrElse result > max) Then
            failReason = OutOfRangeFailureFormat.FormatWith(result, min, max)
        End If
        If failReason Is Nothing Then
            Return Parsed.Success(result)
        ElseIf fallback IsNot Nothing Then
            Return Parsed.Fallback(s, fallback.Value, failReason)
        Else
            Return Parsed.Failed(Of Decimal)(s, failReason)
        End If
    End Function
    Public Function ParseEnum(Of TEnum As Structure)() As TEnum
        ParseEnum(Of TEnum)(Nothing)
    End Function
    Public Function ParseEnum(Of TEnum As Structure)(fallback As TEnum?) As TEnum
        Return HandleParsed(ParsedEnum(GetNextItem(), fallback))
    End Function
    Private Shared Function ParsedEnum(Of TEnum As Structure)(s As String, fallback As TEnum?) As Parsed(Of TEnum)
        Dim result As TEnum = Nothing
        If [Enum].TryParse(s, result) Then
            Return Parsed.Success(result)
        ElseIf fallback IsNot Nothing Then
            Return Parsed.Fallback(s, fallback.Value,
                                   MapFailureFormat.FormatWith(
                                       s,
                                       String.Join(", ", DirectCast([Enum].GetValues(GetType(TEnum)), TEnum()))))
        Else
            Return Parsed.Failed(Of TEnum)(s,
                                           MapFailureFormat.FormatWith(
                                               s,
                                               String.Join(", ", DirectCast([Enum].GetValues(GetType(TEnum)), TEnum()))))
        End If
    End Function
    Public Function Map(Of T As Structure)(mapping As IDictionary(Of String, T)) As T
        Return Map(mapping, Nothing)
    End Function
    Public Function Map(Of T As Structure)(mapping As IDictionary(Of String, T), fallback As T?) As T
        Return HandleParsed(ParsedMap(GetNextItem(), mapping, fallback))
    End Function
    Private Shared Function ParsedMap(Of T As Structure)(s As String, mapping As IDictionary(Of String, T), fallback As T?) As Parsed(Of T)
        Argument.EnsureNotNull(mapping, "mapping")
        Dim result As T = Nothing
        If s IsNot Nothing AndAlso mapping.TryGetValue(s, result) Then
            Return Parsed.Success(result)
        ElseIf fallback IsNot Nothing Then
            Return Parsed.Fallback(s, fallback.Value, MapFailureFormat.FormatWith(s, String.Join(", ", mapping.Keys)))
        Else
            Return Parsed.Failed(Of T)(s, MapFailureFormat.FormatWith(s, String.Join(", ", mapping.Keys)))
        End If
    End Function
    Public Function Project(Of T As Structure)(projection As Func(Of String, T)) As T
        Return Project(projection, Nothing)
    End Function
    Public Function Project(Of T As Structure)(projection As Func(Of String, T), fallback As T?) As T
        Return HandleParsed(ParsedProject(GetNextItem(), projection, fallback))
    End Function
    Private Shared Function ParsedProject(Of T As Structure)(s As String, projection As Func(Of String, T), fallback As T?) As Parsed(Of T)
        Argument.EnsureNotNull(projection, "projection")
        Dim result As T?
        Dim message As String = Nothing
        Try
            result = projection(s)
        Catch ex As Exception
            message = ex.Message
            Dim index = message.IndexOf(Environment.NewLine, StringComparison.CurrentCulture)
            If index <> -1 Then
                message = message.Substring(0, index)
            End If
        End Try
        If result IsNot Nothing Then
            Return Parsed.Success(result.Value)
        ElseIf fallback IsNot Nothing Then
            Return Parsed.Fallback(s, fallback.Value, ProjectFailureFormat.FormatWith(s, message))
        Else
            Return Parsed.Failed(Of T)(s, ProjectFailureFormat.FormatWith(s, message))
        End If
    End Function
    Public Function ParseVector2() As Vector2
        Return ParseVector2(Nothing)
    End Function
    Public Function ParseVector2(fallback As Vector2?) As Vector2
        Return HandleParsed(ParsedVector2(GetNextItem(), fallback))
    End Function
    Private Shared Function ParsedVector2(s As String, fallback As Vector2?) As Parsed(Of Vector2)
        Dim parts As String() = Nothing
        If s IsNot Nothing Then parts = s.Split(","c)
        Dim x As Integer
        Dim y As Integer
        If parts IsNot Nothing AndAlso parts.Length = 2 AndAlso
            Number.TryParseInt32Invariant(parts(0), x) AndAlso
            Number.TryParseInt32Invariant(parts(1), y) Then
            Return Parsed.Success(New Vector2(x, y))
        ElseIf fallback IsNot Nothing Then
            Return Parsed.Fallback(s, fallback.Value, Vector2FailureFormat.FormatWith(s))
        Else
            Return Parsed.Failed(Of Vector2)(s, Vector2FailureFormat.FormatWith(s))
        End If
    End Function
    Public Function SpecifiedCombinePath(pathPrefix As String, source As String) As String
        Return SpecifiedCombinePath(pathPrefix, source, Nothing)
    End Function
    Public Function SpecifiedCombinePath(pathPrefix As String, source As String, fallback As String) As String
        Return HandleParsed(ParsedCombinePath(pathPrefix, source, fallback))
    End Function
    Private Shared Function ParsedCombinePath(pathPrefix As String, s As String, fallback As String) As Parsed(Of String)
        Dim failReasonFormat As String = Nothing
        If s Is Nothing Then failReasonFormat = PathNullFailureFormat
        If failReasonFormat Is Nothing AndAlso
            (s.IndexOfAny(Path.GetInvalidPathChars()) <> -1 OrElse
            s.IndexOfAny(Path.GetInvalidFileNameChars()) <> -1) Then
            failReasonFormat = PathInvalidCharsFailureFormat
        End If
        If failReasonFormat Is Nothing AndAlso Path.IsPathRooted(s) Then failReasonFormat = PathRootedFailureFormat
        If failReasonFormat Is Nothing Then
            Return Parsed.Success(Path.Combine(pathPrefix, s))
        ElseIf fallback IsNot Nothing Then
            Return Parsed.Fallback(s, fallback, failReasonFormat.FormatWith(s))
        Else
            Return Parsed.Failed(Of String)(s, failReasonFormat.FormatWith(s))
        End If
    End Function
    Public Sub SpecifiedFileExists(filePath As String)
        SpecifiedFileExists(filePath, Nothing)
    End Sub
    Public Sub SpecifiedFileExists(filePath As String, fallback As String)
        If File.Exists(filePath) Then
            HandleParsed(Parsed.Success(filePath))
        ElseIf fallback IsNot Nothing Then
            HandleParsed(Parsed.Fallback(filePath, fallback, FileNotFoundFailureFormat.FormatWith(filePath)))
        Else
            HandleParsed(Parsed.Failed(Of String)(filePath, FileNotFoundFailureFormat.FormatWith(filePath)))
        End If
    End Sub
    Public Function Assert(source As String, condition As Boolean, reason As String, fallback As String) As Boolean
        If condition Then
            HandleParsed(Parsed.Success(source))
        ElseIf fallback IsNot Nothing Then
            HandleParsed(Parsed.Fallback(source, fallback, reason))
        Else
            HandleParsed(Parsed.Failed(Of String)(source, reason))
        End If
        Return condition
    End Function

    Protected NotInheritable Class Parsed
        Private Sub New()
        End Sub
        Public Shared Function Success(Of T)(value As T) As Parsed(Of T)
            Return New Parsed(Of T)(value)
        End Function
        Public Shared Function Fallback(Of T)(source As String, value As T, reason As String) As Parsed(Of T)
            Return New Parsed(Of T)(source, value, reason)
        End Function
        Public Shared Function Failed(Of T)(source As String, reason As String, Optional fallback As T = Nothing) As Parsed(Of T)
            Return New Parsed(Of T)(source, reason, fallback)
        End Function
    End Class
    Protected Structure Parsed(Of T)
        Private _source As String
        Private _value As T
        Private _reason As String
        Private _result As ParseResult
        Public ReadOnly Property Source As String
            Get
                If _result = ParseResult.Success Then Throw New InvalidOperationException("Cannot get source for a successful parse.")
                Return _source
            End Get
        End Property
        Public ReadOnly Property Value As T
            Get
                Return _value
            End Get
        End Property
        Public ReadOnly Property Reason As String
            Get
                Return _reason
            End Get
        End Property
        Public ReadOnly Property Result As ParseResult
            Get
                Return _result
            End Get
        End Property
        Public Sub New(_value As T)
            Me._value = _value
            Me._result = ParseResult.Success
        End Sub
        Public Sub New(_source As String, _value As T, _reason As String)
            Me._source = _source
            Me._value = _value
            Me._reason = _reason
            Me._result = ParseResult.Fallback
        End Sub
        Public Sub New(_source As String, _reason As String, _fallback As T)
            Me._source = _source
            Me._reason = _reason
            Me._result = ParseResult.Failed
            Me._value = _fallback
        End Sub
    End Structure
End Class

Public Structure ParseIssue
    Private _index As Integer
    Private _propertyName As String
    Private _source As String
    Private _fallbackValue As String
    Private _reason As String
    Public ReadOnly Property Fatal As Boolean
        Get
            Return _fallbackValue Is Nothing
        End Get
    End Property
    Public ReadOnly Property Index As Integer
        Get
            Return _index
        End Get
    End Property
    Public ReadOnly Property PropertyName As String
        Get
            Return _propertyName
        End Get
    End Property
    Public ReadOnly Property Source As String
        Get
            Return _source
        End Get
    End Property
    Public ReadOnly Property FallbackValue As String
        Get
            Return _fallbackValue
        End Get
    End Property
    Public ReadOnly Property Reason As String
        Get
            Return _reason
        End Get
    End Property
    Public Sub New(_propertyName As String, _source As String, _fallbackValue As String, _reason As String)
        Me.New(-1, _propertyName, _source, _fallbackValue, _reason)
    End Sub
    Public Sub New(_index As Integer, _propertyName As String, _source As String, _fallbackValue As String, _reason As String)
        Me._index = _index
        Me._propertyName = _propertyName
        Me._source = _source
        Me._fallbackValue = _fallbackValue
        Me._reason = _reason
    End Sub
End Structure

Public Enum ParseResult
    Success
    Fallback
    Failed
End Enum

Public Module ParseResultExtensions
    <Extension()>
    Public Function Combine(this As ParseResult, other As ParseResult) As ParseResult
        If this = ParseResult.Failed OrElse other = ParseResult.Failed Then
            Return ParseResult.Failed
        ElseIf this = ParseResult.Fallback OrElse other = ParseResult.Fallback Then
            Return ParseResult.Fallback
        ElseIf this = ParseResult.Success OrElse other = ParseResult.Success Then
            Return ParseResult.Success
        Else
            Return this
        End If
    End Function
End Module
