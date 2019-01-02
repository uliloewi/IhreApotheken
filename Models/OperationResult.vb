Imports System.Runtime.Serialization
<DataContract>
Public Class OperationResult
    <DataMember(Name:="Status", Order:=0)>
    Public Property Status As String = "error"

    <DataMember(Name:="Msg", Order:=1)>
    Public Property Msg As String = ""

    <DataMember(Name:="Result", Order:=1)>
    Public Property Result As Object = Nothing
End Class