using APIHandler.Constants;
using APIHandler.Constants.Orders;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Models.GroupReservation;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Orders;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using APIHandler.Services.Auth;
using APIHandler.Services.GroupReservations;
using APIHandler.Services.Locations;
using AutoFixture.Xunit2;
using Moq;

namespace APIHandler.Tests.Services;

public class GroupReservationServiceTests
{
    private readonly Mock<IGroupReservationRepository> _groupReservationRepositoryMock = new();
    private readonly Mock<ITableRepository> _tableRepositoryMock = new();
    private readonly Mock<IReservationRepository> _reservationRepositoryMock = new();
    private readonly Mock<IGuestSessionRepository> _guestSessionRepositoryMock = new();
    private readonly Mock<IOrderRepository> _orderRepositoryMock = new();
    private readonly Mock<IUserService> _userServiceMock = new();
    private readonly Mock<ILocationService> _locationServiceMock = new();

    private readonly GroupReservationService _service;

    public GroupReservationServiceTests()
    {
        _service = new GroupReservationService(
            _groupReservationRepositoryMock.Object,
            _tableRepositoryMock.Object,
            _reservationRepositoryMock.Object,
            _guestSessionRepositoryMock.Object,
            _orderRepositoryMock.Object,
            _userServiceMock.Object,
            _locationServiceMock.Object);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationByTokenAsync_ValidInput_ReturnsGroupReservationInfo(
        GroupReservation groupReservation,
        Table table,
        Reservation reservation,
        Location location,
        string expectedUserName)
    {
        groupReservation.ReservationId = reservation.Id;
        groupReservation.Status = GroupReservationStatus.Open;
        reservation.TableId = table.Id;
        table.LocationId = location.Id;

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByTokenAsync(groupReservation.ReservationToken))
            .ReturnsAsync(groupReservation);

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _guestSessionRepositoryMock
            .Setup(r => r.CountByGroupReservationId(groupReservation.GroupReservationId))
            .ReturnsAsync(0);

        _tableRepositoryMock
            .Setup(r => r.GetTableAsync(table.Id))
            .ReturnsAsync(table);

        _locationServiceMock
            .Setup(s => s.GetLocationByIdAsync(table.LocationId))
            .ReturnsAsync(location);

        _userServiceMock
            .Setup(s => s.GetUserName(reservation.CustomerId!))
            .ReturnsAsync(expectedUserName);

        var result = await _service.GetGroupReservationByTokenAsync(groupReservation.ReservationToken);

        Assert.Equal(groupReservation.GroupReservationId, result.GroupReservationId);
        Assert.Equal(table.LocationId, result.LocationId);
        Assert.Equal(reservation.GuestCount, result.MaxGuestCount);
        Assert.Equal(reservation.StartTime, result.StartTime);
        Assert.Equal(reservation.EndTime, result.EndTime);
        Assert.Equal(table.TableNumber, result.TableNumber);
        Assert.Equal("Green & Tasty", result.RestaurantName);
        Assert.Equal(location.Address, result.LocationAddress);
        Assert.Equal(expectedUserName, result.InviterName);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationByTokenAsync_InvalidToken_ThrowsNotFound(string token)
    {
        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByTokenAsync(token))
            .ReturnsAsync((GroupReservation?)null);

        await Assert.ThrowsAsync<GroupReservationNotFoundException>(() => _service.GetGroupReservationByTokenAsync(token));
    }

    [Theory]
    [InlineAutoData(GroupReservationStatus.Closed)]
    [InlineAutoData(GroupReservationStatus.Submitted)]
    public async Task GetGroupReservationByTokenAsync_GroupReservationIsNotOpen_ThrowsException(
        GroupReservationStatus status,
        GroupReservation groupReservation)
    {
        groupReservation.Status = status;

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByTokenAsync(groupReservation.ReservationToken))
            .ReturnsAsync(groupReservation);

        await Assert.ThrowsAsync<GroupReservationExpiredException>(() => _service.GetGroupReservationByTokenAsync(groupReservation.ReservationToken));
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationByTokenAsync_WhenGuestCountIsMax_ThrowsGuestLimitExceeded(GroupReservation groupReservation, Reservation reservation)
    {
        groupReservation.Status = GroupReservationStatus.Open;
        groupReservation.ReservationId = reservation.Id;

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByTokenAsync(groupReservation.ReservationToken))
            .ReturnsAsync(groupReservation);

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _guestSessionRepositoryMock
            .Setup(r => r.CountByGroupReservationId(groupReservation.GroupReservationId))
            .ReturnsAsync(reservation.GuestCount);

        await Assert.ThrowsAsync<GuestLimitExceededException>(() => _service.GetGroupReservationByTokenAsync(groupReservation.ReservationToken));
    }

    [Theory]
    [AutoData]
    public async Task CloseGroupReservationAsync_ValidInput_UpdatesGroupReservationStatus(GroupReservation groupReservation)
    {
        _groupReservationRepositoryMock
            .Setup(r => r.GetByReservationIdAsync(groupReservation.ReservationId))
            .ReturnsAsync(groupReservation);

        GroupReservation? savedGroupReservation = null;
        _groupReservationRepositoryMock
            .Setup(r => r.SaveGroupReservationAsync(It.IsAny<GroupReservation>()))
            .Callback<GroupReservation>(r => savedGroupReservation = r);

        await _service.CloseGroupReservationAsync(groupReservation.ReservationId);

        Assert.NotNull(savedGroupReservation);
        Assert.Equal(GroupReservationStatus.Closed, savedGroupReservation.Status);
    }

    [Theory]
    [AutoData]
    public async Task CloseGroupReservationAsync_GroupReservationNotFound_DoesNotThrowException(string reservationId)
    {
        _groupReservationRepositoryMock
            .Setup(r => r.GetByReservationIdAsync(reservationId))
            .ReturnsAsync((GroupReservation?)null);

        await _service.CloseGroupReservationAsync(reservationId);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationInviteTokenAsync_ValidInput_ReturnsToken(Reservation reservation, GroupReservation groupReservation)
    {
        reservation.Status = ReservationStatus.Reserved;
        reservation.GuestCount = 3;
        groupReservation.ReservationId = reservation.Id;
        groupReservation.Status = GroupReservationStatus.Open;

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _groupReservationRepositoryMock
            .Setup(r => r.GetByReservationIdAsync(reservation.Id))
            .ReturnsAsync(groupReservation);

        GetGroupInviteTokenResponse result = await _service.GetGroupReservationInviteTokenAsync(reservation.Id);

        Assert.Equal(groupReservation.ReservationToken, result.Token);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationInviteTokenAsync_InvalidReservationId_ThrowsNotFound(string reservationId)
    {
        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync((Reservation?)null);

        await Assert.ThrowsAsync<ReservationNotFoundException>(() => _service.GetGroupReservationInviteTokenAsync(reservationId));
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationInviteTokenAsync_OneGuest_ThrowsValidationException(Reservation reservation)
    {
        reservation.Status = ReservationStatus.Reserved;
        reservation.GuestCount = 1;

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        await Assert.ThrowsAsync<ValidationException>(() => _service.GetGroupReservationInviteTokenAsync(reservation.Id));
    }

    [Theory]
    [InlineAutoData(ReservationStatus.Completed)]
    [InlineAutoData(ReservationStatus.Cancelled)]
    [InlineAutoData(ReservationStatus.InProgress)]
    public async Task GetGroupReservationInviteTokenAsync_StatusIsNotReserved_ThrowsValidationException(ReservationStatus reservationStatus, Reservation reservation)
    {
        reservation.Status = reservationStatus;
        reservation.GuestCount = 1;

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        await Assert.ThrowsAsync<ValidationException>(() => _service.GetGroupReservationInviteTokenAsync(reservation.Id));
    }

    [Theory]
    [InlineAutoData(GroupReservationStatus.Closed, typeof(GroupReservationExpiredException))]
    [InlineAutoData(GroupReservationStatus.Submitted, typeof(GroupReservationSubmittedException))]
    public async Task GetGroupReservationInviteTokenAsync_GroupStatusIsNotOpen_ThrowsException(GroupReservationStatus status, Type type, Reservation reservation, GroupReservation groupReservation)
    {
        reservation.Status = ReservationStatus.Reserved;
        reservation.GuestCount = 3;
        groupReservation.ReservationId = reservation.Id;
        groupReservation.Status = status;

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _groupReservationRepositoryMock
            .Setup(r => r.GetByReservationIdAsync(reservation.Id))
            .ReturnsAsync(groupReservation);

        await Assert.ThrowsAsync(type, () => _service.GetGroupReservationInviteTokenAsync(reservation.Id));
    }

    private GroupReservation PrepareOpenGroupForSubmit(string ownerId)
    {
        GroupReservation groupReservation = new()
        {
            GroupReservationId = "group-1",
            ReservationId = "reservation-1",
            ReservationToken = "token-1",
            Status = GroupReservationStatus.Open
        };

        Reservation reservation = new()
        {
            Id = "reservation-1",
            TableId = "table-1",
            WaiterId = "waiter-1",
            CustomerId = ownerId
        };

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync(groupReservation);

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        return groupReservation;
    }

    private static GuestSession Session(string id, bool isSubmitted, bool isOwner, params (string DishId, int Quantity)[] dishes) => new()
    {
        Id = id,
        GuestName = isOwner ? "Owner" : id,
        GroupReservationId = "group-1",
        Dishes = dishes.Select(d => new OrderItem { DishId = d.DishId, Quantity = d.Quantity }).ToList(),
        IsSubmitted = isSubmitted,
        IsOwnerSession = isOwner
    };

    [Fact]
    public async Task SubmitGroupReservationAsync_TwoSubmittedSessions_MergesDishesAndSubmitsOrder()
    {
        const string ownerId = "owner-1";
        GroupReservation groupReservation = PrepareOpenGroupForSubmit(ownerId);

        List<GuestSession> sessions =
        [
            Session("session-a", isSubmitted: true, isOwner: false, ("dish-1", 2)),
            Session("session-b", isSubmitted: true, isOwner: false, ("dish-1", 1), ("dish-2", 2)),
            Session("session-c", isSubmitted: false, isOwner: false, ("dish-3", 9)),
            Session("owner-session", isSubmitted: true, isOwner: true, ("dish-4", 5))
        ];

        Order order = new()
        {
            OrderId = "order-1",
            CustomerId = ownerId,
            ReservationId = groupReservation.ReservationId,
            ReservationAddress = "Some street 19, Table 1",
            ReservationTime = "Oct 14, 2025, 9:00 a.m.",
            Dishes = [new OrderItem { DishId = "dish-1", Quantity = 1, State = OrderItemState.Pending }],
            Status = OrderStatus.Created
        };

        _guestSessionRepositoryMock
            .Setup(r => r.GetAllByGroupReservationIdAsync("group-1"))
            .ReturnsAsync(sessions);

        _orderRepositoryMock
            .Setup(r => r.GetOrderByReservationIdAsync(groupReservation.ReservationId))
            .ReturnsAsync(order);

        _orderRepositoryMock
            .Setup(r => r.SaveOrderAsync(order))
            .Returns(Task.CompletedTask);

        GroupReservation? savedGroup = null;
        _groupReservationRepositoryMock
            .Setup(r => r.SaveGroupReservationAsync(It.IsAny<GroupReservation>()))
            .Callback<GroupReservation>(g => savedGroup = g)
            .Returns(Task.CompletedTask);

        List<MergedDishResponse> result = (await _service.SubmitGroupReservationAsync(ownerId, "group-1", "reservation-1")).ToList();

        Assert.Equal(2, result.Count);
        Assert.Equal(3, result.Single(d => d.DishId == "dish-1").Quantity);
        Assert.Equal(2, result.Single(d => d.DishId == "dish-2").Quantity);

        Assert.Equal(4, order.Dishes.Single(d => d.DishId == "dish-1").Quantity);
        Assert.Equal(2, order.Dishes.Single(d => d.DishId == "dish-2").Quantity);
        Assert.Equal(OrderStatus.SubmittedByCustomer, order.Status);
        Assert.Equal(GroupReservationStatus.Submitted, savedGroup!.Status);
        _orderRepositoryMock.Verify(r => r.SaveOrderAsync(order), Times.Once());
    }

    [Fact]
    public async Task SubmitGroupReservationAsync_CallerNotOwner_ThrowsActionForbiddenException()
    {
        PrepareOpenGroupForSubmit("real-owner");

        await Assert.ThrowsAsync<ActionForbiddenException>(
            () => _service.SubmitGroupReservationAsync("caller-not-owner", "group-1", "reservation-1"));
    }

    [Fact]
    public async Task SubmitGroupReservationAsync_GroupAlreadySubmitted_ThrowsGroupReservationSubmittedException()
    {
        GroupReservation groupReservation = PrepareOpenGroupForSubmit("owner-1");
        groupReservation.Status = GroupReservationStatus.Submitted;

        await Assert.ThrowsAsync<GroupReservationSubmittedException>(
            () => _service.SubmitGroupReservationAsync("owner-1", "group-1", "reservation-1"));
    }

    [Fact]
    public async Task SubmitGroupReservationAsync_GroupClosed_ThrowsGroupReservationExpiredException()
    {
        GroupReservation groupReservation = PrepareOpenGroupForSubmit("owner-1");
        groupReservation.Status = GroupReservationStatus.Closed;

        await Assert.ThrowsAsync<GroupReservationExpiredException>(
            () => _service.SubmitGroupReservationAsync("owner-1", "group-1", "reservation-1"));
    }

    [Fact]
    public async Task SubmitGroupReservationAsync_NoSubmittedSessions_ThrowsValidationException()
    {
        PrepareOpenGroupForSubmit("owner-1");

        _guestSessionRepositoryMock
            .Setup(r => r.GetAllByGroupReservationIdAsync("group-1"))
            .ReturnsAsync([Session("session-a", isSubmitted: false, isOwner: false, ("dish-1", 1))]);

        await Assert.ThrowsAsync<ValidationException>(
            () => _service.SubmitGroupReservationAsync("owner-1", "group-1", "reservation-1"));
    }

    [Fact]
    public async Task SubmitGroupReservationAsync_SubmittedSessionsHaveNoDishes_ThrowsValidationException()
    {
        PrepareOpenGroupForSubmit("owner-1");

        _guestSessionRepositoryMock
            .Setup(r => r.GetAllByGroupReservationIdAsync("group-1"))
            .ReturnsAsync([Session("session-a", isSubmitted: true, isOwner: false)]);

        await Assert.ThrowsAsync<ValidationException>(
            () => _service.SubmitGroupReservationAsync("owner-1", "group-1", "reservation-1"));
    }

    [Fact]
    public async Task SubmitGroupReservationAsync_OrderMissing_ThrowsValidationException()
    {
        PrepareOpenGroupForSubmit("owner-1");

        _guestSessionRepositoryMock
            .Setup(r => r.GetAllByGroupReservationIdAsync("group-1"))
            .ReturnsAsync([Session("session-a", isSubmitted: true, isOwner: false, ("dish-1", 1))]);

        _orderRepositoryMock
            .Setup(r => r.GetOrderByReservationIdAsync("reservation-1"))
            .ReturnsAsync((Order?)null);

        await Assert.ThrowsAsync<ValidationException>(
            () => _service.SubmitGroupReservationAsync("owner-1", "group-1", "reservation-1"));
    }

    [Fact]
    public async Task SubmitGroupReservationAsync_UnknownGroup_ThrowsGroupReservationNotFoundException()
    {
        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByIdAsync("unknown-group"))
            .ReturnsAsync((GroupReservation?)null);

        await Assert.ThrowsAsync<GroupReservationNotFoundException>(
            () => _service.SubmitGroupReservationAsync("owner-1", "unknown-group", "reservation-1"));
    }
}
