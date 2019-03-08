Imports MySql.Data.MySqlClient

Public Module DataProccessingHelper
    Public Function PharmacyJoinOrganisation() As String
        Return IIf(conMain.StartsWith("Server=vertriebsappprod"),
                   "  INNER JOIN e_apothekerstammdaten a on (g.PharmacyID=a.PharmacyID)  ",
               " INNER JOIN e_customeraccount_ref r on (g.PharmacyID=r.PharmacyID)
                 INNER JOIN e_apothekerstammdaten a on (a.ApothekenID=r.ApothekendeID)  ")
    End Function

    Public Function OrderJoinContract() As String
        'Return " INNER JOIN i_contract c on (c.ContractID = o.ContractID) "
        Return " INNER JOIN c_customeraccount t on (t.CustomerAccountID = o.CustomerAccountID) 
                 INNER JOIN i_contract c on (c.ContractID = t.ContractID)    "
    End Function

    Public Sub SetPropertyValues(ByRef o As Object, myreader As MySqlDataReader, Optional specialProp As Dictionary(Of String, String) = Nothing)
        For Each prop In o.GetType.GetProperties
            Dim colName = prop.Name
            If specialProp IsNot Nothing AndAlso specialProp.ContainsKey(prop.Name) Then
                colName = specialProp(prop.Name)
            End If
            If Enumerable.Range(0, myreader.FieldCount).Any(Function(i) myreader.GetName(i).ToLower = colName.ToLower) AndAlso Not IsDBNull(myreader(colName)) Then
                If prop.PropertyType Is GetType(String) Then
                    prop.SetValue(o, myreader(colName).ToString)
                ElseIf prop.PropertyType Is GetType(Boolean) Then
                    prop.SetValue(o, CType(myreader(colName), Boolean))
                Else
                    prop.SetValue(o, myreader(colName))
                End If
            End If
        Next
    End Sub


    Public Function BuildInsertString(tableName As String, obj As Object, Optional idColumnName As String = Nothing) As String
        Dim myQuery As String = " Insert into " + tableName + "("
        For Each prop In obj.GetType.GetProperties
            Dim value = prop.GetValue(obj)
            If Not IsNothing(value) And prop.Name <> idColumnName Then
                myQuery = myQuery + prop.Name + ","
            End If
        Next
        If myQuery.EndsWith(",") Then
            myQuery = myQuery.Remove(myQuery.Length - 1)
        End If
        myQuery = myQuery + (") VALUES ('")
        For Each prop In obj.GetType.GetProperties
            Dim value = prop.GetValue(obj)
            If Not IsNothing(value) And prop.Name <> idColumnName Then
                myQuery = myQuery + value + "','"
            End If
        Next
        If myQuery.EndsWith(",'") Then
            myQuery = myQuery.Remove(myQuery.Length - 2)
        End If
        myQuery = myQuery + " )"
        Return myQuery
    End Function

    Friend Function MD5Create(ByVal Input As String, Optional Salt As String = "aa35f0e3e15") As String
        Dim MD5 As New System.Security.Cryptography.MD5CryptoServiceProvider()
        MD5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(Input & Salt))
        Dim data() As Byte = MD5.Hash

        Dim sBuilder As New System.Text.StringBuilder()

        ' Loop through each byte of the hashed data  
        ' and format each one as a hexadecimal string. 
        Dim i As Integer
        For i = 0 To data.Length - 1
            sBuilder.Append(data(i).ToString("x2"))
        Next i

        ' Return the hexadecimal string. 
        Return sBuilder.ToString()
    End Function
End Module
