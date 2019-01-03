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
                res.Msg = "No Authorization"
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
                    cmd.Parameters.AddWithValue("inFunctionID", re.FunctionID)
                    Dim foaID As Integer = cmd.ExecuteScalar
                    If foaID <> 0 Then
                        token = GenerateRandomString(20, False)
                        cmd.Parameters.Clear()
                        cmd.CommandText = "save_functiontoken"
                        cmd.Parameters.AddWithValue("inFoaID", foaID)
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


        <HttpGet>
        <Route("PharmacyID")>
        Public Function GetPharmacyID() As HttpResponseMessage
            Dim authkey As String = ""
            Dim res As New OperationResult
            If Request.Headers.Contains("AuthKey") Then
                authkey = Request.Headers.GetValues("AuthKey").First
            End If
            Dim ApoID As Integer
            Try
                Using conn As New MySqlConnection(config.conMain)
                    conn.Open()
                    Using cmd As New MySqlCommand
                        cmd.CommandTimeout = config.dbTimeout
                        cmd.CommandType = CommandType.StoredProcedure
                        cmd.Connection = conn
                        cmd.CommandText = "token_isvalid"
                        cmd.Parameters.AddWithValue("inToken", authkey)
                        ApoID = cmd.ExecuteScalar
                        If ApoID = 0 Then
                            res.Status = HttpStatusCode.Unauthorized
                            Throw New HttpResponseException(res.Status)
                        Else
                            res.Status = HttpStatusCode.OK
                            res.Result = ApoID
                        End If
                    End Using
                    conn.Close()
                End Using
            Catch ex As Exception
                res.Msg = ex.Message
                res.Result = -1
            End Try
            Return Request.CreateResponse(res.Status, res)
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