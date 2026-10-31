namespace APIHandler.Exceptions;

public class GroupReservationNotOpenException()
    : ValidationException("Guest sessions can only be deleted while the group reservation is open.");
