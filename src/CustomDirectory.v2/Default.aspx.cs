using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

using System.Xml;

using System.Net;
using System.IO;

namespace CustomDirectory.v2
{
    public partial class _Default : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            #region QueryString
            string first = Request.QueryString["f"];
            string last = Request.QueryString["l"];
            string pais = Request.QueryString["p"];
            string number = Request.QueryString["n"];

            if (first == null) first = "";
            if (last == null) last = "";
            if (pais == null) pais = "";
            if (number == null) number = "";
            #endregion

            //fiddler
            string url = "", cadena_chile = "", cadena_arg = "", cadena_ambos = "";
            bool vacio = false;
            if (pais.Trim() == string.Empty)
            {
                #region Chile
                url = "http://10.11.75.10:8080/ccmcip/xmldirectorylist.jsp?l=" + last + "&f=" + first + "&n=" + number;
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                StreamReader sr = new StreamReader(response.GetResponseStream());
                cadena_chile = sr.ReadToEnd();
                sr.Close();
                cadena_chile = cadena_chile.Replace("<?xml version=\"1.0\"?>", "").Replace("<Telephone>", "<Telephone>*18").Replace("<CiscoIPPhoneDirectory>", "").
                    Replace("<Name>", "<Name>[CL] ");

                for (int i = 0; i < cadena_chile.Length; i++)
                    if (cadena_chile[i].ToString() == "<" && cadena_chile[i + 1].ToString() == "P")
                        cadena_chile = cadena_chile.Substring(0, i);

                int cant = 0;
                for (int i = 0; i < cadena_chile.Length; i++)
                {
                    if (cadena_chile[i].ToString() == "<" && cadena_chile[i + 1].ToString() == "D")
                        cant++;
                    if (cant == 16)
                        cadena_chile = cadena_chile.Substring(0, i);
                }
                #endregion

                #region Argentina
                url = "http://10.11.147.10:8080/ccmcip/xmldirectorylist.jsp?l=" + last + "&f=" + first + "&n=" + number;
                request = (HttpWebRequest)WebRequest.Create(url);
                response = (HttpWebResponse)request.GetResponse();
                sr = new StreamReader(response.GetResponseStream());
                cadena_arg = sr.ReadToEnd();
                sr.Close();
                cadena_arg = cadena_arg.Replace("<?xml version=\"1.0\"?>", "").Replace("<CiscoIPPhoneDirectory>", "").Replace("<Name>", "<Name>[ARG] ").Replace("Garc�a", "Garcia");

                for (int i = 0; i < cadena_arg.Length; i++)
                    if (cadena_arg[i].ToString() == "<" && cadena_arg[i + 1].ToString() == "P")
                        cadena_arg = cadena_arg.Substring(0, i);

                cant = 0;
                for (int i = 0; i < cadena_arg.Length; i++)
                {
                    if (cadena_arg[i].ToString() == "<" && cadena_arg[i + 1].ToString() == "D")
                        cant++;
                    if (cant == 16)
                        cadena_arg = cadena_arg.Substring(0, i);
                }
                #endregion

                cadena_ambos = cadena_chile + Environment.NewLine + cadena_arg;
            }
            else
            {
                #region Evalua filtrado por Chile
                bool igual_chile = true;
                int b = 0;
                string country = "chile";
                while (igual_chile == true && b < pais.Length)
                {
                    if (pais[b].ToString() != country[b].ToString())
                        igual_chile = false;
                    b++;
                }
                #endregion

                if (igual_chile == true || pais.ToLower() == "cl")
                {
                    #region Chile
                    url = "http://10.11.75.10:8080/ccmcip/xmldirectorylist.jsp?l=" + last + "&f=" + first + "&n=" + number;
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                    HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                    StreamReader sr = new StreamReader(response.GetResponseStream());
                    cadena_chile = sr.ReadToEnd();
                    sr.Close();
                    cadena_chile = cadena_chile.Replace("<?xml version=\"1.0\"?>", "").Replace("<Telephone>", "<Telephone>18562").Replace("<CiscoIPPhoneDirectory>", "").
                                   Replace("<Name>", "<Name>[CL] ");

                    for (int i = 0; i < cadena_chile.Length; i++)
                        if (cadena_chile[i].ToString() == "<" && cadena_chile[i + 1].ToString() == "P")
                            cadena_chile = cadena_chile.Substring(0, i);
                    #endregion
                }
                else
                {
                    #region Evalua filtrado por argentina
                    bool igual_arg = true;
                    b = 0;
                    country = "argentina";
                    while (igual_arg == true && b < pais.Length)
                    {
                        if (pais[b].ToString().ToLower() != country[b].ToString())
                            igual_arg = false;
                        b++;
                    }
                    #endregion

                    if (igual_arg)
                    {
                        #region Argentina
                        url = "http://10.11.147.10:8080/ccmcip/xmldirectorylist.jsp?l=" + last + "&f=" + first + "&n=" + number;
                        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                        HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                        StreamReader sr = new StreamReader(response.GetResponseStream());
                        cadena_arg = sr.ReadToEnd();
                        sr.Close();
                        cadena_arg = cadena_arg.Replace("<?xml version=\"1.0\"?>", "").Replace("<CiscoIPPhoneDirectory>", "").Replace("<Name>", "<Name>[ARG] ").Replace("Garc�a", "Garcia");

                        for (int i = 0; i < cadena_arg.Length; i++)
                            if (cadena_arg[i].ToString() == "<" && cadena_arg[i + 1].ToString() == "P")
                                cadena_arg = cadena_arg.Substring(0, i);
                        #endregion
                    }
                    else
                        vacio = true;
                }
            }


            string final = string.Empty, leyenda = string.Empty;
            if (!vacio)
            {
                if (cadena_arg != string.Empty && cadena_chile != string.Empty)
                {
                    final = cadena_ambos;
                    leyenda = "Registros de Chile y Argentina";
                }
                else if (cadena_arg != string.Empty && cadena_chile == string.Empty)
                {
                    final = cadena_arg;
                    leyenda = "Registros Argentina";
                }
                else if (cadena_arg == string.Empty && cadena_chile != string.Empty)
                {
                    final = cadena_chile;
                    leyenda = "Registros Chile";
                }

                #region Pie de pagina
                final = "<CiscoIPPhoneDirectory>" + Environment.NewLine + final +
                "<Prompt>" + leyenda + "</Prompt>" + Environment.NewLine +
                "<SoftKeyItem>" + Environment.NewLine +
                    "<Name>Llamar</Name>" + Environment.NewLine +
                    "<URL>SoftKey:Dial</URL>" + Environment.NewLine +
                    "<Position>1</Position>" + Environment.NewLine +
                "</SoftKeyItem>" + Environment.NewLine +
                "<SoftKeyItem>" + Environment.NewLine +
                    "<Name>Editar</Name>" + Environment.NewLine +
                    "<URL>SoftKey:EditDial</URL>" + Environment.NewLine +
                    "<Position>2</Position>" + Environment.NewLine +
                "</SoftKeyItem>" + Environment.NewLine +
                "<SoftKeyItem>" + Environment.NewLine +
                    "<Name>Salir</Name>" + Environment.NewLine +
                    "<URL>SoftKey:Exit</URL>" +
                    "<Position>3</Position>" + Environment.NewLine +
                "</SoftKeyItem>" + Environment.NewLine +
                "</CiscoIPPhoneDirectory>";
                #endregion
            }
            else
                final = "<CiscoIPPhoneDirectory><Prompt>Busqueda sin coincidencias</Prompt></CiscoIPPhoneDirectory>";

            Response.ContentType = "text/xml";
            Response.Write(final);
        }
    }
}