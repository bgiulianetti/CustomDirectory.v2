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
            int start = Int32.Parse(Request.QueryString["start"]);
            #endregion


            string cadena_chile = "", cadena_argentina = "", cadena_ambos = "";
            bool vacio = false;
            if (string.Equals(string.Empty, pais))
            {
                cadena_chile = GetDirectory("chile", last, first, number, start.ToString());
                cadena_argentina = GetDirectory("argentina", last, first, number, start.ToString());
                cadena_ambos = cadena_chile + Environment.NewLine + cadena_argentina;
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
                    cadena_chile = GetDirectory("chile", last, first, number, start.ToString());
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
                        cadena_argentina = GetDirectory("argentina", last, first, number, start.ToString());
                    }
                    else
                        vacio = true;
                }
            }


            string final = string.Empty, leyenda = string.Empty;
            if (!vacio)
            {
                leyenda = GetCountrySearchCriteriaTitle(cadena_argentina, cadena_chile, cadena_ambos, final);
                GetMenu(final, leyenda);
            }
            else
                final = "<CiscoIPPhoneDirectory><Prompt>Busqueda sin coincidencias</Prompt></CiscoIPPhoneDirectory>";

            Response.ContentType = "text/xml";
            Response.Write(final);
        }


        private string GetDirectory(string pais, string last, string first, string number, string start)
        {
            string url =GetDirectoryUrlByCountry(pais) + last + "&f=" + first + "&n=" + number + "&start=" + start;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            StreamReader sr = new StreamReader(response.GetResponseStream());
            string cadena = sr.ReadToEnd();
            sr.Close();

            cadena = cadena.Replace("<?xml version=\"1.0\"?>", "").Replace("<CiscoIPPhoneDirectory>", "").Replace("<Name>", "<Name>[ARG] ").Replace("Garc�a", "Garcia").Replace("<Telephone>", "<Telephone>" + GetPrefixByCountry(pais));

            for (int i = 0; i < cadena.Length; i++)
                if (cadena[i].ToString() == "<" && cadena[i + 1].ToString() == "P")
                    cadena = cadena.Substring(0, i);

            int cant = 0;
            for (int i = 0; i < cadena.Length; i++)
            {
                if (cadena[i].ToString() == "<" && cadena[i + 1].ToString() == "D")
                    cant++;
                if (cant == 16)
                    cadena = cadena.Substring(0, i);
            }
            return cadena;
        }
        private string GetDirectoryUrlByCountry(string country)
        {
            if (string.Equals(country, "argentina", StringComparison.InvariantCultureIgnoreCase))
                return System.Configuration.ConfigurationManager.AppSettings.Get("DirectoryUrlArgentina");
            else
                return System.Configuration.ConfigurationManager.AppSettings.Get("DirectoryUrlChile");
        }
        private string GetPrefixByCountry(string country)
        {
            if (string.Equals(country, "argentina", StringComparison.InvariantCultureIgnoreCase))
                return System.Configuration.ConfigurationManager.AppSettings.Get("Prefix_Argentina");
            else
                return System.Configuration.ConfigurationManager.AppSettings.Get("Prefix_Chile");
        }
        private string GetCountrySearchCriteriaTitle(string cadena_argentina, string cadena_chile, string cadena_ambos, string final)
        {
            string leyenda = string.Empty;
            if (cadena_argentina != string.Empty && cadena_chile != string.Empty)
            {
                final = cadena_ambos;
                leyenda = "Registros de Chile y Argentina";
            }
            else if (cadena_argentina != string.Empty && cadena_chile == string.Empty)
            {
                final = cadena_argentina;
                leyenda = "Registros Argentina";
            }
            else if (cadena_argentina == string.Empty && cadena_chile != string.Empty)
            {
                final = cadena_chile;
                leyenda = "Registros Chile";
            }
            return leyenda;
        }
        private string GetMenu(string final, string leyenda)
        {
            return "<CiscoIPPhoneDirectory>" + Environment.NewLine + final +
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


                "<SoftKeyItem>" + Environment.NewLine +
                "<Name>Next</Name>" + Environment.NewLine +
                "<URL>" + System.Configuration.ConfigurationManager.AppSettings.Get("SelfUrl") + "?start=32</URL>" + Environment.NewLine +
                "<Position>4</Position>" + Environment.NewLine +
                "</SoftKeyItem>" + Environment.NewLine +

                "<SoftKeyItem>" + Environment.NewLine +
                "<Name>Search</Name>" + Environment.NewLine +
                "<URL>" + System.Configuration.ConfigurationManager.AppSettings.Get("SelfUrl") + "</URL>" + Environment.NewLine +
                "<Position>5</Position>" + Environment.NewLine +
                "</SoftKeyItem>" + Environment.NewLine +
                "</CiscoIPPhoneDirectory>";
        }
    }
}