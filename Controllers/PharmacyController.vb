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
                        cmd.ExecuteNonQuery()
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
            Dim opres As New OperationResult
            Dim res As New List(Of Integer)
            Try
                Using conn As New MySqlConnection(conMain)
                    conn.Open()
                    Using cmd As New MySqlCommand()
                        Dim myQuery As String = "SELECT ApothekendeID FROM crm.e_customeraccount_ref"
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
            Dim opres As New OperationResult
            If Authentification(Me.Request) Then
                Dim res As New Pharmacy
                Try
                    Using conn As New MySqlConnection(conMain)
                        conn.Open()
                        Using cmd As New MySqlCommand()
                            Dim myQuery As String = "SELECT * FROM e_apothekerstammdaten where pharmacyid =" + pharmacyid
                            cmd.CommandText = myQuery
                            cmd.Connection = conn
                            Dim myReader = cmd.ExecuteReader()
                            While myReader.Read()
                                For Each prop In res.GetType.GetProperties
                                    If Enumerable.Range(0, myReader.FieldCount).Any(Function(i) myReader.GetName(i) = prop.Name) AndAlso Not IsDBNull(myReader(prop.Name)) Then
                                        If prop.PropertyType Is GetType(String) Then
                                            prop.SetValue(res, myReader(prop.Name).ToString)
                                        Else
                                            prop.SetValue(res, myReader(prop.Name))
                                        End If
                                    End If
                                Next
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
            Else
                opres.Status = HttpStatusCode.Unauthorized
                opres.Msg = "Keine Authorisation"
            End If
            Return Request.CreateResponse(opres.Status, opres)
        End Function

        <HttpGet>
        <Route("pharmacies/{apoid}/orderitems")>
        Public Function GetOrderDetails(apoid As String) As HttpResponseMessage
            Dim opres As New OperationResult
            If Authentification(Me.Request) Then
                Dim res As New List(Of OrderItem)
                Try
                    Using conn As New MySqlConnection(conMain)
                        conn.Open()
                        Using cmd As New MySqlCommand()
                            Dim myQuery As String = "SELECT distinct i.OrderItemID, i.OrderID, p.ProductID, i.Quantity, i.CreatedByID, i.DateUpdate,
                     i.UpdatedByID, i.OrderItemVersion, i.OrderItemUpdateDate, p.ProductName, o.OrderDate FROM i_orderitem i  
                    INNER JOIN i_order o on (o.OrderID = i.OrderID) 
                    INNER JOIN p_productoffering f on (f.ProductOfferingID = i.ProductOfferingID) 
                    INNER JOIN p_productcatalog l on (l.ProductCatalogID = f.ProductCatalogID)
                    INNER JOIN p_product p on (p.ProductID = l.ProductID)
                    INNER JOIN i_contract c on (c.ContractID = o.ContractID)
                    INNER JOIN o_organisation g on (c.PartyID = g.PartyID)
                    where g.PharmacyID =" + apoid
                            cmd.CommandText = myQuery
                            cmd.Connection = conn
                            Dim myReader = cmd.ExecuteReader()
                            While myReader.Read()
                                Dim r As New OrderItem
                                For Each prop In r.GetType.GetProperties
                                    If Enumerable.Range(0, myReader.FieldCount).Any(Function(i) myReader.GetName(i) = prop.Name) AndAlso Not IsDBNull(myReader(prop.Name)) Then
                                        If prop.PropertyType Is GetType(String) Then
                                            prop.SetValue(r, myReader(prop.Name).ToString)
                                        Else
                                            prop.SetValue(r, myReader(prop.Name))
                                        End If
                                    End If
                                Next
                                res.Add(r)
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
            Else
                opres.Status = HttpStatusCode.Unauthorized
                opres.Msg = "Keine Authorisation"
            End If
            Return Request.CreateResponse(opres.Status, opres)
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
                            Dim orderid As String = ""
                            Dim myQuery As String = "SELECT distinct o.* FROM p_ProductCatalog p 
                     INNER JOIN p_productoffering f on (f.ProductCatalogID = p.ProductCatalogID)
                     INNER JOIN i_orderitem i  on (f.ProductOfferingID = i.ProductOfferingID)
                     INNER JOIN i_order o  on (o.OrderID = i.OrderID)                    
                     INNER JOIN c_customeraccount t on (t.CustomerAccountID = o.CustomerAccountID) 
                 INNER JOIN i_contract c on (c.ContractID = t.ContractID)
                     INNER JOIN o_organisation g on (c.PartyID = g.PartyID)                      
                     where g.PharmacyID= '" + pharmacyid + "' And p.Productid ='" + productid + "' and o.orderstateid not in (10,4,999,99999)"
                            cmd.CommandText = myQuery
                            cmd.Connection = conn
                            Dim myReader = cmd.ExecuteReader()
                            While myReader.Read()
                                orderid = myReader("OrderID").ToString
                            End While
                            myReader.Close()
                            If (orderid <> "") Then
                                myQuery = "SELECT u.*,t.* FROM crm.i_artefact_attributes u 
                                inner join crm.i_attributetype t on (u.attribute_typeID=t.attribute_typeID)
                                inner join crm.i_order_associatedartefacts a on (u.OrderArtefactID=a.OrderArtefactID) 
                                inner join crm.i_orderassociation s on (a.orderassociationid=s.orderassociationid)
                                inner join crm.i_order o on (s.Orderid= o.Orderid)
                                where o.orderid=" + orderid + " and a.orderworkflowid='" + workflowid + "'"
                                cmd.CommandText = myQuery
                                cmd.Connection = conn
                                myReader = cmd.ExecuteReader()
                                While myReader.Read()
                                    Dim arte As Artefact = artefactList.Where(Function(a) a.OrderArtefactID = myReader("OrderArtefactID")).FirstOrDefault
                                    If arte Is Nothing Then
                                        arte = New Artefact With {.OrderArtefactID = myReader("OrderArtefactID")}
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
                        sqlTran = conn.BeginTransaction()
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
                     INNER JOIN i_contract c on (c.ContractID = t.ContractID)
                     INNER JOIN o_organisation g on (c.PartyID = g.PartyID)                      
                     where g.PharmacyID= '" + pharmacyid + "' And p.Productid ='" + productid + "' and o.orderstateid not in (10,4,999,99999)"
                        selectCmd.CommandText = myQuery
                        selectCmd.Connection = conn
                        Dim myReader = selectCmd.ExecuteReader()
                        While myReader.Read()
                            orderid = myReader("OrderID").ToString
                        End While
                        myReader.Close()
                        If (orderid <> "") Then
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
                            Dim maxArtefactID As String = getMaxArtefactID(selectCmd, conn, myReader, workflowid)
                            myQuery = "INSERT INTO i_order_associatedartefacts (OrderassociationID, ArtefactTypeID, ArtefactStateID, OrderWorkflowID, ParentOrderArtefactID)  VALUES (" +
                                maxAssociationID + ", 2, 1001, " + workflowid + "," + maxArtefactID + ");"
                            insertCmd.CommandText = myQuery
                            insertCmd.Connection = conn
                            insertCmd.ExecuteNonQuery()
                            maxArtefactID = getMaxArtefactID(selectCmd, conn, myReader, workflowid)
                            myQuery = "INSERT INTO i_artefact_attributes (OrderArtefactID, attribute_typeID, attribute_value)  VALUES (" + maxArtefactID + ", 1, '" + arte.Versionsnummer + "');
                            INSERT INTO i_artefact_attributes (OrderArtefactID, attribute_typeID, attribute_value)  VALUES (" + maxArtefactID + ", 2, '" + arte.Dateinamen.Replace("\", "\\") + "');"
                            insertCmd.CommandText = myQuery
                            insertCmd.Connection = conn
                            insertCmd.ExecuteNonQuery()
                            opres.Status = HttpStatusCode.Created
                        Else
                            opres.Status = HttpStatusCode.NotFound
                        End If
                        sqlTran.Commit()
                        GetArtefactsOfOrder(pharmacyid, productid, workflowid).TryGetContentValue(opres)
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
                        Return False
                    End Using
                End Using
            Else
                Return False
            End If
        End Function

    End Class
End Namespace