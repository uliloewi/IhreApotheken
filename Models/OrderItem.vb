Imports System.Runtime.Serialization

<DataContract>
Public Class OrderItem
    <DataMember>
    Public Property OrderItemID As Integer 'from orderitem
    <DataMember>
    Public Property OrderID As String  'from orderitem
    <DataMember>
    Public Property Quantity As String  'from orderitem
    <DataMember>
    Public Property ProductID As String  'from orderitem
    <DataMember>
    Public Property ProductName As String  'from product
    <DataMember>
    Public Property OrderDate As String  'from order
End Class
