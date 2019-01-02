Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Net.Http.Headers
Imports System.Web.Http
Imports System.Web.Http.Cors

Public Module WebApiConfig
    Public Sub Register(ByVal config As HttpConfiguration)

        ' Web-API-Routen
        config.MapHttpAttributeRoutes()

        config.EnableCors(New EnableCorsAttribute("*", "*", "*"))

        config.Routes.MapHttpRoute(
            name:="DefaultApi",
            routeTemplate:="api/{controller}/{id}",
            defaults:=New With {.kdnr = RouteParameter.Optional}
        )

    End Sub
End Module
