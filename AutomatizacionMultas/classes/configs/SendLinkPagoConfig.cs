//SendLinkPagoConfig.cs

namespace AutomatizacionMultas.classes.configs
{
    public class SendLinkPagoConfig: SeleniumConfig
    {
        public HellehollisConnectionConfig HellehollisConnection { get; set; } = default!;
    }


    public class HellehollisConnectionConfig : ConnectionConfig
    {

    }


}   