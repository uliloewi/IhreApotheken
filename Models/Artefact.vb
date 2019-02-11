Imports System.Runtime.Serialization

<DataContract()>
Public Class Artefact
    <DataMember>
    Public Property OrderArtefactID As Integer
    <DataMember>
    Public Property Versionsnummer As String
    <DataMember>
    Public Property Dateinamen As String
    <DataMember>
    Public Property ParentOrderArtefactID As Integer

End Class
