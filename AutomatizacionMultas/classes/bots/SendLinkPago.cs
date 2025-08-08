//SendLinkPago.cs

using AutomatizacionMultas.classes.configs;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UglyToad.PdfPig;

namespace AutomatizacionMultas.classes.bots
{
    internal class SendLinkPago : SeleniumBot<SendLinkPagoConfig>
    {
        // ↓ Solo utilidades internas (no config):
        private static readonly Random Rnd = new();

        public SendLinkPago(SendLinkPagoConfig? cfg) : base(cfg)
        {

        }

        protected override Task ExecuteAsync()
        {
            throw new NotImplementedException();
        }
    }
}