Imports System.Net
Imports System.Net.Http
Imports System.Web.Http
Imports System.Web.Http.Cors
Imports MySql.Data
Imports MySql.Data.MySqlClient

Namespace Controllers
    <RoutePrefix("api")>
    Public Class PharmacyController
        Inherits ApiController

        <HttpPost>
        <Route("token")>
        Public Function GetToken(foa As FunctionOfPharmacy) As HttpResponseMessage
            Dim res As New OperationResult
            If Authentification(Me.Request) Then
                CreateToken(conMain, foa, res)
            Else
                res.Status = HttpStatusCode.Unauthorized
                res.Msg = "Keine Authorisation"
            End If
            Return Request.CreateResponse(res.Status, res)
        End Function

        Private Sub CreateToken(connstring As String, re As FunctionOfPharmacy, ByRef res As OperationResult)
            Dim token As String = ""
            Using conn As New MySqlConnection(connstring)
                conn.Open()
                Using cmd As New MySqlCommand
                    cmd.CommandTimeout = config.dbTimeout
                    cmd.CommandType = CommandType.StoredProcedure
                    cmd.Connection = conn
                    cmd.CommandText = "fuction_of_pharmacy_isvalid"
                    cmd.Parameters.AddWithValue("inApoID", re.ApoID)
                    Dim apothekendeID As Integer = cmd.ExecuteScalar
                    If apothekendeID <> 0 Then
                        token = GenerateRandomString(20, False)
                        cmd.Parameters.Clear()
                        cmd.CommandText = "save_functiontoken"
                        cmd.Parameters.AddWithValue("inFoaID", apothekendeID) 'ApothekendeID in e_customeraccount_ref
                        cmd.Parameters.AddWithValue("inToken", token)
                        Dim myReader As MySqlDataReader = cmd.ExecuteReader
                        While myReader.Read()
                            token = myReader(0)
                        End While
                        res.Status = HttpStatusCode.OK
                        res.Result = token
                        res.Msg = ""
                    Else
                        res.Status = HttpStatusCode.BadRequest
                        res.Msg = "Apotheke order Funktion ist falsch!"
                    End If
                End Using
            End Using
        End Sub


        Private Function GenerateRandomString(ByRef len As Integer, ByRef upper As Boolean) As String
            Dim rand As New Random()
            Dim allowableChars() As Char = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLOMNOPQRSTUVWXYZ0123456789".ToCharArray()
            Dim final As String = String.Empty
            For i As Integer = 0 To len - 1
                final += allowableChars(rand.Next(allowableChars.Length - 1))
            Next
            Return IIf(upper, final.ToUpper(), final)
        End Function


        <HttpPost>
        <Route("PharmacyID")>
        Public Function GetPharmacyID(functiontoken As FunctionToken) As HttpResponseMessage
            Dim res As New OperationResult
            If Authentification(Me.Request) Then
                Dim found As Boolean = False
                Dim accountRef As New CustomerAccountRef

                Try
                    Using conn As New MySqlConnection(config.conMain)
                        conn.Open()
                        Using cmd As New MySqlCommand
                            cmd.CommandTimeout = config.dbTimeout
                            cmd.CommandType = CommandType.StoredProcedure
                            cmd.Connection = conn
                            cmd.CommandText = "token_isvalid"
                            cmd.Parameters.AddWithValue("inToken", functiontoken.token)
                            Dim myReader As MySqlDataReader = cmd.ExecuteReader
                            While myReader.Read()
                                accountRef.ApothekendeID = myReader("ApothekendeID")
                                accountRef.PharmacyID = myReader("PharmacyID")
                                res.Result = accountRef
                                res.Status = HttpStatusCode.OK
                                found = True
                            End While
                            If Not found Then
                                res.Status = HttpStatusCode.NotFound
                            End If
                        End Using
                        conn.Close()
                    End Using
                Catch ex As Exception
                    If res.Msg = "" Then res.Msg = ex.Message
                    res.Result = -1
                End Try
            Else
                res.Status = HttpStatusCode.Unauthorized
                res.Msg = "Keine Authorisation"
            End If
            Return Request.CreateResponse(res.Status, res)
        End Function

        <HttpGet>
        <Route("Apothekendeids")>
        Public Function GetApothekendeIDs() As HttpResponseMessage
            Dim myQuery As String = "SELECT ApothekendeID FROM crm.e_customeraccount_ref"
            'Return GetList(Of Int16)(myQuery)
            Dim opres As New OperationResult
            Dim res As New List(Of Integer)
            Try
                Using conn As New MySqlConnection(conMain)
                    conn.Open()
                    Using cmd As New MySqlCommand()
                        cmd.CommandText = myQuery
                        cmd.Connection = conn
                        Dim myReader = cmd.ExecuteReader()
                        While myReader.Read()
                            res.Add(CInt(myReader(0)))
                        End While
                    End Using
                    conn.Close()
                End Using
                opres.Result = res
                opres.Status = HttpStatusCode.OK
            Catch ex As Exception
                opres.Status = HttpStatusCode.InternalServerError
                opres.Msg = ex.Message
                opres.Result = Nothing
            End Try
            Return Request.CreateResponse(opres.Status, opres)
        End Function

        <HttpGet>
        <Route("pharmacies/{pharmacyid}")>
        Public Function GetPharmacyByID(pharmacyid As String) As HttpResponseMessage
            Dim myQuery As String = "SELECT * FROM e_apothekerstammdaten where pharmacyid =" + pharmacyid
            Return GetAnObject(Of Pharmacy)(myQuery)
        End Function

        <HttpGet>
        <Route("pharmacies/{pharmacyid}/orderitems")>
        Public Function GetOrderDetails(pharmacyid As String) As HttpResponseMessage
            Dim myQuery As String = "SELECT distinct i.OrderItemID, i.OrderID, p.ProductID, i.Quantity, i.CreatedByID, i.DateUpdate,
                     i.UpdatedByID, i.OrderItemVersion, p.ProductName, o.OrderDate FROM i_orderitem i  
                    INNER JOIN i_order o on (o.OrderID = i.OrderID) 
                    INNER JOIN p_productoffering f on (f.ProductOfferingID = i.ProductOfferingID) 
                    INNER JOIN p_productcatalog l on (l.ProductCatalogID = f.ProductCatalogID)
                    INNER JOIN p_product p on (p.ProductID = l.ProductID)
                    inner join c_customeraccount c on (o.customeraccountid=c.customeraccountid)
                    INNER JOIN o_organisation g on (c.PartyID = g.PartyID)
                    where i.orderItemStateID not in (4, 10) and o.orderstateid not in (4,10) and g.PharmacyID =" + pharmacyid
            Return GetList(Of OrderItem)(myQuery)
        End Function

        Private Function OldQueryOfArtefacts(pharmacyid As String, productid As String, cmd As MySqlCommand, conn As MySqlConnection, artefactList As List(Of Artefact)) As Artefact
            Dim orderid As String = ""
            Dim res As New Artefact
            Dim myQuery As String = "SELECT distinct o.* FROM p_ProductCatalog p 
                     INNER JOIN p_productoffering f on (f.ProductCatalogID = p.ProductCatalogID)
                     INNER JOIN i_orderitem i  on (f.ProductOfferingID = i.ProductOfferingID)
                     INNER JOIN i_order o  on (o.OrderID = i.OrderID)                    
                     INNER JOIN c_customeraccount t on (t.CustomerAccountID = o.CustomerAccountID) 
                     INNER JOIN o_organisation g on (t.PartyID = g.PartyID)                      
                     where g.PharmacyID= '" + pharmacyid + "' And p.Productid ='" + productid + "' and o.orderstateid not in (10,4,999,99999)"
            cmd.CommandText = myQuery
            cmd.Connection = conn
            Dim myReader = cmd.ExecuteReader()
            While myReader.Read()
                orderid = myReader("OrderID").ToString
            End While
            myReader.Close()
            If (orderid <> "") Then
                myQuery = "SELECT u.*,t.*, s.AssociationDate FROM crm.i_artefact_attributes u 
                                inner join crm.i_attributetype t on (u.attribute_typeID=t.attribute_typeID)
                                inner join crm.i_order_associatedartefacts a on (u.OrderArtefactID=a.OrderArtefactID) 
                                inner join crm.i_orderassociation s on (a.orderassociationid=s.orderassociationid)
                                inner join crm.i_order o on (s.Orderid= o.Orderid)
                                where o.orderid=" + orderid + " and a.orderworkflowid='30004'"
                cmd.CommandText = myQuery
                cmd.Connection = conn
                myReader = cmd.ExecuteReader()
                While myReader.Read()
                    Dim arte As Artefact = artefactList.Where(Function(a) a.OrderArtefactID = myReader("OrderArtefactID")).FirstOrDefault
                    If arte Is Nothing Then
                        arte = New Artefact With {.OrderArtefactID = myReader("OrderArtefactID"), .AssociationDate = myReader("AssociationDate").ToString.Substring(0, 16)}
                        artefactList.Add(arte)
                    End If
                    For Each prop In arte.GetType.GetProperties
                        If myReader("attributeCaption") = prop.Name Then
                            prop.SetValue(arte, myReader("attribute_value").ToString)
                        End If
                    Next
                End While
                myReader.Close()
                res = artefactList.OrderBy(Function(x) CDbl(x.Versionsnummer)).LastOrDefault()
            End If
            Return res
        End Function

        <HttpGet>
        <Route("pharmacies/{pharmacyid}/orderedproducts/{productid}/workflows/{workflowid}/artefacts")>
        Public Function GetArtefactsOfOrder(pharmacyid As String, productid As String, workflowid As String) As HttpResponseMessage
            Dim opres As New OperationResult
            If Authentification(Me.Request) Then
                Dim artefactList As New List(Of Artefact)
                Dim res As New Artefact
                Try
                    Using conn As New MySqlConnection(conMain)
                        conn.Open()
                        Using cmd As New MySqlCommand()
#Region "to delete in the future"
                            res = OldQueryOfArtefacts(pharmacyid, productid, cmd, conn, artefactList)
                            If res IsNot Nothing AndAlso res.OrderArtefactID > 0 Then
                                opres.Result = res
                                opres.Status = HttpStatusCode.OK
                                Return Request.CreateResponse(opres.Status, opres)
                            End If
#End Region
                            Dim myReader = cmd.ExecuteReader()
                            myReader.Close()
                            Dim partyid = GetPartyID(cmd, conn, myReader, pharmacyid)
                            Dim foundArtefactID As String
                            Dim myQuery = "select Max(OrderArtefactID) from i_artefact_attributes  where attribute_typeid=4 and attribute_value= " + partyid + " and OrderArtefactID in (
                            select OrderArtefactID from i_artefact_attributes  where attribute_typeid=5 and attribute_value= " + productid + " ) limit 1"
                            cmd.CommandText = myQuery
                            cmd.Connection = conn

                            myReader = cmd.ExecuteReader()
                            While myReader.Read()
                                If myReader(0).ToString = "" Then
                                    opres.Status = HttpStatusCode.NotFound
                                    opres.Msg = "Kein Artefakt gefunden."
                                    Return Request.CreateResponse(opres.Status, opres)
                                Else
                                    foundArtefactID = myReader(0)
                                End If
                            End While
                            myReader.Close()

                            myQuery = "SELECT u.*,t.*, s.AssociationDate FROM crm.i_artefact_attributes u 
                                inner join crm.i_attributetype t on (u.attribute_typeID=t.attribute_typeID)
                                inner join crm.i_order_associatedartefacts a on (u.OrderArtefactID=a.OrderArtefactID) 
                                inner join crm.i_orderassociation s on (a.orderassociationid=s.orderassociationid)
                                where a.orderartefactid=" + foundArtefactID + " and a.orderworkflowid='" + workflowid + "'"
                            cmd.CommandText = myQuery
                            cmd.Connection = conn
                            myReader = cmd.ExecuteReader()
                            While myReader.Read()
                                Dim arte As Artefact = artefactList.Where(Function(a) a.OrderArtefactID = myReader("OrderArtefactID")).FirstOrDefault
                                If arte Is Nothing Then
                                    arte = New Artefact With {.OrderArtefactID = myReader("OrderArtefactID"), .AssociationDate = myReader("AssociationDate").ToString.Substring(0, 16)}
                                    artefactList.Add(arte)
                                End If
                                For Each prop In arte.GetType.GetProperties
                                    If myReader("attributeCaption") = prop.Name Then
                                        prop.SetValue(arte, myReader("attribute_value").ToString)
                                    End If
                                Next
                            End While
                            res = artefactList.OrderBy(Function(x) CDbl(x.Versionsnummer)).LastOrDefault()
                            If res IsNot Nothing Then
                                opres.Result = res
                                opres.Status = HttpStatusCode.OK
                            Else
                                opres.Status = HttpStatusCode.NotFound
                            End If

                        End Using
                        conn.Close()
                    End Using
                Catch ex As Exception
                    opres.Status = HttpStatusCode.InternalServerError
                    opres.Msg = ex.Message
                    opres.Result = Nothing
                End Try
            Else
                opres.Status = HttpStatusCode.Unauthorized
                opres.Msg = "Keine Authorisation"
            End If
            Return Request.CreateResponse(opres.Status, opres)
        End Function

        <HttpPost>
        <Route("pharmacies/{pharmacyid}/orderedproducts/{productid}/workflows/{workflowid}/artefacts")>
        Public Function AddArtefactToOrder(pharmacyid As String, productid As String, workflowid As String, arte As Artefact) As HttpResponseMessage
            Dim opres As New OperationResult
            Dim sqlTran As MySqlTransaction
            If Authentification(Me.Request) Then
                Try
                    Using conn As New MySqlConnection(conMain)
                        conn.Open()
                        Dim selectCmd As MySqlCommand = conn.CreateCommand()
                        Dim insertCmd As MySqlCommand = conn.CreateCommand()
                        selectCmd.Transaction = sqlTran
                        insertCmd.Transaction = sqlTran
                        Dim orderid As String = ""
                        Dim myQuery As String = "SELECT distinct o.* FROM p_ProductCatalog p 
                         INNER JOIN p_productoffering f on (f.ProductCatalogID = p.ProductCatalogID)
                         INNER JOIN i_orderitem i  on (f.ProductOfferingID = i.ProductOfferingID)
                         INNER JOIN i_order o  on (o.OrderID = i.OrderID)                    
                         INNER JOIN c_customeraccount t on (t.CustomerAccountID = o.CustomerAccountID) 
                         INNER JOIN o_organisation g on (t.PartyID = g.PartyID)                      
                         where g.PharmacyID= '" + pharmacyid + "' And p.Productid ='" + productid + "' and o.orderstateid not in (10,4,999,99999) limit 1"
                        selectCmd.CommandText = myQuery
                        selectCmd.Connection = conn
                        Dim myReader = selectCmd.ExecuteReader()
                        While myReader.Read()
                            orderid = myReader("OrderID").ToString
                        End While
                        myReader.Close()
                        Dim partyid = GetPartyID(selectCmd, conn, myReader, pharmacyid)
                        If (orderid <> "") Then
                            sqlTran = conn.BeginTransaction()
                            Dim maxAssociationID As String = ""
                            myQuery = "INSERT INTO i_orderassociation (OrderID, OrderassociationtypeID, AssociationDate, PartyID)  VALUES (" + orderid + ", 5, NOW(), 572);"
                            insertCmd.CommandText = myQuery
                            insertCmd.Connection = conn
                            insertCmd.ExecuteNonQuery()
                            myQuery = "SELECT max(OrderassociationID) FROM i_orderassociation;"
                            selectCmd.CommandText = myQuery
                            selectCmd.Connection = conn
                            myReader = selectCmd.ExecuteReader()
                            While myReader.Read()
                                maxAssociationID = myReader(0).ToString
                            End While
                            myReader.Close()
                            GetArtefactsOfOrder(pharmacyid, productid, workflowid).TryGetContentValue(opres)
                            If IsNothing(opres.Result) Then
                                myQuery = "INSERT INTO i_order_associatedartefacts (OrderassociationID, ArtefactTypeID, ArtefactStateID, OrderWorkflowID)  VALUES (" +
                                maxAssociationID + ", 2, 1001, " + workflowid + ");"
                            Else
                                myQuery = "INSERT INTO i_order_associatedartefacts (OrderassociationID, ArtefactTypeID, ArtefactStateID, OrderWorkflowID, ParentOrderArtefactID)  VALUES (" +
                                maxAssociationID + ", 2, 1001, " + workflowid + "," + CType(opres.Result, Artefact).OrderArtefactID.ToString + ");"
                            End If
                            insertCmd.CommandText = myQuery
                            insertCmd.Connection = conn
                            insertCmd.ExecuteNonQuery()
                            Dim maxArtefactID = getMaxArtefactID(selectCmd, conn, myReader, workflowid)
                            myQuery = "INSERT INTO i_artefact_attributes (OrderArtefactID, attribute_typeID, attribute_value)  VALUES (" + maxArtefactID + ", 1, '" + arte.Versionsnummer + "');
                            INSERT INTO i_artefact_attributes (OrderArtefactID, attribute_typeID, attribute_value)  VALUES (" + maxArtefactID + ", 2, '" + arte.Dateinamen.Replace("\", "\\") + "');
                            INSERT INTO i_artefact_attributes (OrderArtefactID, attribute_typeID, attribute_value)  VALUES (" + maxArtefactID + ", 3, '" + arte.Dateinamenvorschau.Replace("\", "\\") + "');
                            INSERT INTO i_artefact_attributes (OrderArtefactID, attribute_typeID, attribute_value)  VALUES (" + maxArtefactID + ", 4, '" + partyid + "');
                            INSERT INTO i_artefact_attributes (OrderArtefactID, attribute_typeID, attribute_value)  VALUES (" + maxArtefactID + ", 5, '" + productid + "');"
                            insertCmd.CommandText = myQuery
                            insertCmd.Connection = conn
                            insertCmd.ExecuteNonQuery()
                            opres.Status = HttpStatusCode.Created
                            sqlTran.Commit()
                            GetArtefactsOfOrder(pharmacyid, productid, workflowid).TryGetContentValue(opres)
                        Else
                            opres.Status = HttpStatusCode.NotFound
                            opres.Msg = "Keine Bestellung gefunden. Kein Artefakt gespeichert."
                        End If
                        conn.Close()
                    End Using
                Catch ex As Exception
                    If sqlTran IsNot Nothing Then
                        sqlTran.Connection.Open()
                        sqlTran.Rollback()
                    End If
                    opres.Status = HttpStatusCode.InternalServerError
                    opres.Msg = ex.Message
                    opres.Result = Nothing
                End Try
            Else
                opres.Status = HttpStatusCode.Unauthorized
                opres.Msg = "Keine Authorisation"
            End If
            Return Request.CreateResponse(opres.Status, opres)
        End Function

        Private Function getMaxArtefactID(selectCmd, conn, myReader, workflowID) As String
            Dim maxArtefactID As String
            Dim myQuery As String = "Select max(OrderArtefactID) FROM i_order_associatedartefacts where OrderWorkflowID=" + workflowID
            selectCmd.CommandText = myQuery
            selectCmd.Connection = conn
            myReader = selectCmd.ExecuteReader()
            While myReader.Read()
                maxArtefactID = myReader(0).ToString
            End While
            myReader.Close()
            Return maxArtefactID
        End Function

        Private Function GetPartyID(selectCmd, conn, myReader, pharmacyID) As String
            Dim partyID As String
            Dim myQuery As String = "SELECT distinct o.PartyID FROM o_organisation o where o.pharmacyid= '" + pharmacyID + "' limit 1"
            selectCmd.CommandText = myQuery
            selectCmd.Connection = conn
            myReader = selectCmd.ExecuteReader()
            While myReader.Read()
                partyID = myReader(0).ToString
            End While
            myReader.Close()
            Return partyID
        End Function

        <HttpGet>
        <Route("tokentest/{tk}")>
        Public Function CheckToken(tk As String) As HttpResponseMessage
            Return GetAnObject(Of CheckToken)("SELECT if(count(*) = 0, false, true) as isvalid FROM o_functiontoken where token= '" & tk & "' and  DATE_ADD(datecreated, INTERVAL 2 day) >= now() limit 1")
        End Function

        Private Function Authentification(rq As Http.HttpRequestMessage) As Boolean
            If rq.Headers.Contains("Authorization") Then
                Dim authkey = Request.Headers.GetValues("Authorization").First
                Using conn As New MySqlConnection(config.conMain)
                    conn.Open()
                    Using cmd As New MySqlCommand()
                        cmd.CommandText = "SELECT *  FROM o_authkey where authorizationkey = '" + authkey + "'"
                        cmd.Connection = conn
                        Dim myReader = cmd.ExecuteReader()
                        While myReader.Read()
                            Return True
                        End While
                        Throw New HttpResponseException(HttpStatusCode.Unauthorized)
                    End Using
                End Using
            Else
                Throw New HttpResponseException(HttpStatusCode.Unauthorized)
                Return False
            End If
        End Function


        Private Function GetList(Of T As {New})(myQuery As String, Optional dict As Dictionary(Of String, String) = Nothing) As HttpResponseMessage
            Authentification(Me.Request)
            Dim res As New List(Of T)
            Dim opres As New OperationResult
            opres.Status = HttpStatusCode.NotFound
            Try
                Using conn As New MySqlConnection(conMain)
                    conn.Open()
                    Using cmd As New MySqlCommand()
                        cmd.CommandText = myQuery
                        cmd.Connection = conn
                        Dim myReader = cmd.ExecuteReader()
                        While myReader.Read()
                            Dim r As New T
                            SetPropertyValues(r, myReader, dict)
                            res.Add(r)
                            opres.Status = HttpStatusCode.OK
                        End While
                    End Using
                    conn.Close()
                End Using
                opres.Result = res
            Catch ex As Exception
                opres.Status = HttpStatusCode.InternalServerError
                opres.Msg = ex.Message
                opres.Result = Nothing
            End Try
            Return Request.CreateResponse(opres.Status, opres)
        End Function

        Private Function GetAnObject(Of T As {New})(myQuery As String, Optional dict As Dictionary(Of String, String) = Nothing)
            Authentification(Me.Request).ToString()
            Dim res As New T
            Dim opres As New OperationResult
            opres.Status = HttpStatusCode.NotFound
            Try
                Using conn As New MySqlConnection(conMain)
                    conn.Open()
                    Using cmd As New MySqlCommand()
                        cmd.CommandText = myQuery
                        cmd.Connection = conn
                        Dim myReader = cmd.ExecuteReader()
                        While myReader.Read()
                            SetPropertyValues(res, myReader)
                            opres.Status = HttpStatusCode.OK
                        End While
                    End Using
                    conn.Close()
                End Using
                opres.Result = res
            Catch ex As Exception
                opres.Status = HttpStatusCode.InternalServerError
                opres.Msg = ex.Message
                opres.Result = Nothing
            End Try
            Return Request.CreateResponse(opres.Status, opres)
        End Function

        Private Function UpdateAnObject(Of T As {New})(tableName As String, idName As String, whereCondiction As String, queryResult As String, ct As T, Optional trailedID As String = "")
            Dim pid = Authentification(Me.Request).ToString()
            Dim opres As New OperationResult
            opres.Status = HttpStatusCode.OK
            Dim myQuery = "update " + tableName + " set "
            Dim res As New T
            Dim sqlTran As MySqlTransaction
            Dim trails As New List(Of String)
            Dim OldValue = "select columnName from " + tableName + " " + whereCondiction + " limit 1"
            Try
                Using conn As New MySqlConnection(conMain)
                    conn.Open()
                    sqlTran = conn.BeginTransaction()
                    Dim cmd As MySqlCommand = conn.CreateCommand
                    cmd.Transaction = sqlTran
                    For Each prop In ct.GetType.GetProperties
                        Dim value = prop.GetValue(ct)
                        If Not IsNothing(value) And prop.Name <> idName Then
                            Select Case prop.Name
                                Case "PasswordHash"
                                    myQuery = myQuery + prop.Name + "='" + MD5Create(value.ToString) + "',"
                                    Dim trail = "Insert into i_audittrail (AuditTable,TableID,AuditColumn,OldValue,NewValue,ChangedBy,Action) values 
                                                     ('" + tableName + "'," + trailedID + ",'" + prop.Name + "','',''," & pid & ",'Update');"
                                    trails.Add(trail)
                                Case Else
                                    myQuery = myQuery + prop.Name + "='" + value + "',"
                                    If trailedID <> "" Then
                                        Dim trail = "Insert into i_audittrail (AuditTable,TableID,AuditColumn,OldValue,NewValue,ChangedBy,Action) values 
                                                     ('" + tableName + "'," + trailedID + ",'" + prop.Name + "',(" + OldValue.Replace("columnName", prop.Name) + "),'" + value + "'," & pid & ",'Update');"
                                        trails.Add(trail)
                                    End If
                            End Select
                        End If
                    Next
                    If myQuery.EndsWith(",") Then myQuery = myQuery.Remove(myQuery.Length - 1)
                    myQuery = myQuery + whereCondiction
                    If trails.Count > 0 Then myQuery = String.Join("", trails) + myQuery
                    cmd.CommandText = myQuery
                    cmd.Connection = conn
                    cmd.ExecuteNonQuery()
                    sqlTran.Commit()
                    cmd.CommandText = queryResult
                    cmd.Connection = conn
                    Dim myReader = cmd.ExecuteReader()
                    While myReader.Read()
                        SetPropertyValues(res, myReader)
                        opres.Status = HttpStatusCode.OK
                    End While
                    conn.Close()
                End Using
                opres.Result = res
            Catch ex As Exception
                If sqlTran IsNot Nothing Then
                    sqlTran.Connection.Open()
                    sqlTran.Rollback()
                End If
                opres.Status = HttpStatusCode.InternalServerError
                opres.Msg = ex.Message
                opres.Result = Nothing
            End Try
            Return Request.CreateResponse(opres.Status, opres)
        End Function


        Private Function CreateAnObject(Of T As {New})(insertQuery As String, selectQuery As String, Optional audittrailedTables As Dictionary(Of String, String) = Nothing)
            Dim personid = Authentification(Me.Request).ToString()
            Dim res As New T
            Dim opres As New OperationResult
            opres.Status = HttpStatusCode.OK
            Dim sqlTran As MySqlTransaction
            Try
                Using conn As New MySqlConnection(conMain)
                    conn.Open()
                    sqlTran = conn.BeginTransaction()
                    Dim cmd As MySqlCommand = conn.CreateCommand
                    cmd.Transaction = sqlTran
                    cmd.CommandText = insertQuery
                    cmd.Connection = conn
                    cmd.ExecuteNonQuery()
                    If audittrailedTables IsNot Nothing Then
                        Dim trailquery = ""
                        For Each tablename In audittrailedTables.Keys
                            trailquery = trailquery & "Insert into i_audittrail (AuditTable,TableID,AuditColumn,OldValue,NewValue,ChangedBy,Action) values ('" & tablename & "',( select max(" & audittrailedTables(tablename) & ") from " & tablename & " ) ,'*','','...'," & personid & ",'Create');"
                        Next
                        cmd.CommandText = trailquery
                        cmd.Connection = conn
                        cmd.ExecuteNonQuery()
                    End If
                    sqlTran.Commit()
                    cmd.CommandText = selectQuery
                    Dim myReader = cmd.ExecuteReader()
                    While myReader.Read()
                        SetPropertyValues(res, myReader)
                    End While
                    conn.Close()
                End Using
                opres.Result = res
            Catch ex As Exception
                If sqlTran IsNot Nothing Then
                    sqlTran.Connection.Open()
                    sqlTran.Rollback()
                End If
                opres.Status = HttpStatusCode.InternalServerError
                opres.Msg = ex.Message
                opres.Result = Nothing
            End Try
            Return Request.CreateResponse(opres.Status, opres)
        End Function
    End Class
End Namespace