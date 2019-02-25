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
            If Enumerable.Range(0, myreader.FieldCount).Any(Function(i) myreader.GetName(i) = colName) AndAlso Not IsDBNull(myreader(colName)) Then
                If prop.PropertyType Is GetType(String) Then
                    prop.SetValue(o, myreader(colName).ToString) 'ToDo ApothekenID_old = ApothekenID
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
End Module
