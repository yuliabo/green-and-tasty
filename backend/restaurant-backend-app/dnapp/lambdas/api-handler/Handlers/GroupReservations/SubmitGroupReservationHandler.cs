using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Helpers;
using APIHandler.Models.GroupReservation;
using APIHandler.Services.GroupReservations;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace APIHandler.Handlers.GroupReservations;

public class SubmitGroupReservationHandler(IGroupReservationService groupReservationService) : IApiRequestHandler
{
    public async Task<APIGatewayProxyResponse> HandleRequest(APIGatewayProxyRequest request, ILambdaContext context)
    {
        if (!request.TryGetPathValue("groupReservationId", out string? groupReservationId))
            return ResponseBuilder.CreateErrorResponse(404, "Group reservation was not found", context.Logger);

        if (request.RequestContext?.Authorizer?.Claims == null ||
            !request.RequestContext.Authorizer.Claims.TryGetValue("sub", out var userId))
            return ResponseBuilder.CreateErrorResponse(401, "Unauthorized.", context.Logger);

        if (!RequestBodyHelper.TryParseAndValidate(request, context, out SubmitGroupReservationRequest? requestBody, out APIGatewayProxyResponse? errorResponse))
            return errorResponse;

        try
        {
            IEnumerable<MergedDishResponse> mergedDishes = await groupReservationService.SubmitGroupReservationAsync(
                userId, groupReservationId, requestBody.ReservationId);

            return ResponseBuilder.CreateJsonResponse(200, mergedDishes);
        }
        catch (GroupReservationNotFoundException exception)
        {
            return ResponseBuilder.CreateErrorResponse(404, exception.Message, context.Logger);
        }
        catch (ReservationNotFoundException exception)
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
