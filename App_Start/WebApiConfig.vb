Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Net.Http.Headers
Imports System.Web.Http
Imports System.Web.Http.Cors

Public Module WebApiConfig
    Public Sub Register(ByVal config As HttpConfiguration)
        ' Web-API-Konfiguration und -Dienste
        config.Formatters.JsonFormatter.SupportedMediaTypes.Add(New MediaTypeHeaderValue("text/plain"))
        ' Web-API-Routen
        config.MapHttpAttributeRoutes()

        config.EnableCors(New EnableCorsAttribute("*", "*", "*"))

        config.Routes.MapHttpRoute(
            name:="DefaultApi",
            routeTemplate:="api/{controller}/{kdnr}",
            defaults:=New With {.kdnr = RouteParameter.Optional}
        )

        config.Routes.MapHttpRoute(
            name:="FormBuilderOrderApi",
            routeTemplate:="api/{controller}/orderid/{orderid}",
            defaults:=New With {.orderid = RouteParameter.Optional}
        )

        config.Routes.MapHttpRoute(
            name:="FormBuilderApi",
            routeTemplate:="api/{controller}/{id}/{kdnr}",
            defaults:=New With {.id = RouteParameter.Optional, .kdnr = RouteParameter.Optional}
        )

        config.Routes.MapHttpRoute(
            name:="OrdersApi",
            routeTemplate:="api/{controller}/all/{strSearch}",
            defaults:=New With {.strSearch = RouteParameter.Optional}
        )


    End Sub
End Module
