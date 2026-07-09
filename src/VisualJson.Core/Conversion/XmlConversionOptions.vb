' SPDX-License-Identifier: MPL-2.0
Namespace Conversion
    ''' FR-P2-601: array expansion mode for JSON to XML conversion.
    Public Enum XmlArrayMode
        ''' Default: arrays expand as repeated <item> elements (v1.0.0 behavior).
        ItemElements
        ''' "tags":[1,2] becomes <tags>1</tags><tags>2</tags>. Arrays without a
        ''' parent name (root arrays, arrays inside arrays) still use <item>.
        RepeatParentName
    End Enum

    ''' FR-P2-601: null representation for JSON to XML conversion.
    Public Enum XmlNullMode
        ''' Default: null becomes an empty element (v1.0.0 behavior).
        EmptyElement
        ''' null becomes <x xsi:nil="true"/> with xmlns:xsi declared on the root.
        XsiNil
    End Enum

    ''' Options are chosen in the conversion preview and are not persisted;
    ''' every export starts from the defaults (spec 06 §4).
    Public Class XmlConversionOptions
        Public Property ArrayMode As XmlArrayMode = XmlArrayMode.ItemElements
        Public Property NullMode As XmlNullMode = XmlNullMode.EmptyElement

        Public Shared Function CreateDefault() As XmlConversionOptions
            Return New XmlConversionOptions()
        End Function
    End Class
End Namespace
