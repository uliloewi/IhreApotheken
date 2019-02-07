Imports System.Runtime.Serialization

<DataContract()>
Public Class FunctionOfPharmacy
    <DataMember>
    Public Property FunctionofapoID As Integer
    <DataMember>
    Public Property FunctionID As String
    <DataMember>
    Public Property ApoID As String 'ApothekendeID in e_customeraccount_ref
End Class
