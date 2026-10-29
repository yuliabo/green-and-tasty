using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Helpers;
using APIHandler.Models.GuestSessions;
using APIHandler.Services.GuestSessions;
using System.Threading.Tasks;

namespace APIHandler.Handlers.GuestSessions;

public class GetReservationGuestSessionIdHandler(IGuestSessionService guestSessionService) : IApiRequestHandler
{
    public async Task<APIGatewayProxyResponse> HandleRequest(APIGatewayProxyRequest request, ILambdaContext context)
    {
        if (!request.TryGetPathValue("reservationId", out string? reservationId))
            return ResponseBuilder.CreateErrorResponse(404, "Reservation was not found", context.Logger);

        if (request.RequestContext?.Authorizer?.Claims == null ||
            !request.RequestContext.Authorizer.Claims.TryGetValue("sub", out var userId))
            return ResponseBuilder.CreateErrorResponse(401, "Unauthorized.", context.Logger);

        try
        {
            string guestSessionId = await guestSessionService.GetOrCreateOwnerSessionIdAsync(userId, reservationId);
            return ResponseBuilder.CreateJsonResponse(200, new OwnerSessionIdResponse(guestSessionId));
        }
        catch (ReservationNotFoundException exception)
        {
            return ResponseBuilder.CreateErrorResponse(404, exception.Message, context.Logger);
        }
        catch (GroupReservationNotFoundException exception)
        {
            return ResponseBuilder.CreateErrorResponse(404, exception.Message, context.Logger);
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
