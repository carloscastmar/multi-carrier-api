using MultiCarrierApi.Infrastructure.Identity;

namespace MultiCarrierApi.Web.Endpoints;

public class Users : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        // Bearer tokens are stateless, so there is no server-side logout: clients discard their tokens.
        groupBuilder.MapIdentityApi<ApplicationUser>();
    }
}
