Imports System.Net
Imports System.Runtime.Serialization
<DataContract>
Public Class OperationResult
    <DataMember(Name:="Status", Order:=0)>
    Public Property Status As HttpStatusCode = HttpStatusCode.Ambiguous

    <DataMember(Name:="Msg", Order:=1)>
    Public Property Msg As String = ""

    <DataMember(Name:="Result", Order:=2)>
    Public Property Result As Object = Nothing
End Class