using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Helpers;
using APIHandler.Models.GuestSessions;
using APIHandler.Services.GuestSessions;
using System.Threading.Tasks;

namespace APIHandler.Handlers.GuestSessions;

public class GetGroupReservationSessionsHandler(IGuestSessionService guestSessionService) : IApiRequestHandler
{
    public async Task<APIGatewayProxyResponse> HandleRequest(APIGatewayProxyRequest request, ILambdaContext context)
    {
        if (!request.TryGetPathValue("groupReservationId", out string? groupReservationId))
            return ResponseBuilder.CreateErrorResponse(404, "Group reservation not found", context.Logger);

        if (request.RequestContext?.Authorizer?.Claims == null ||
            !request.RequestContext.Authorizer.Claims.TryGetValue("sub", out var userId))
            return ResponseBuilder.CreateErrorResponse(401, "Unauthorized.", context.Logger);

        try
        {
            GetAllSessionsResponse result = await guestSessionService.GetAllSessionsAsync(userId, groupReservationId);
            return ResponseBuilder.CreateJsonResponse(200, result);
        }
        catch (GroupReservationNotFoundException exception)
        {
            return ResponseBuilder.CreateJsonResponse(404, exception.Message);
        }
        catch (ActionForbiddenException exception)
        {
            return ResponseBuilder.CreateErrorResponse(403, exception.Message, context.Logger);
        }
        catch (ValidationException exception)
        {
            return ResponseBuilder.CreateErrorCodeResponse(exception);
        }
    }
}
