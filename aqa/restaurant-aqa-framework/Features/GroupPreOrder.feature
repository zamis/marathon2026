Feature: Group pre-order
  As the owner of a reservation, I want to review and submit my guests' carts,
  so that the restaurant receives one accurate group order.

  Rule: Level 1 — only the reservation owner may review participants and totals

    @api @data-dependent
    Scenario: Owner sees item quantities, cart states and recalculated totals
      Given the owner has created a group pre-order
      And "Ava" has added 2 dishes but has not confirmed the cart
      And "Ben" has added 1 dishes and confirmed the cart
      And "Casey" has joined without adding dishes
      When the owner reviews the participants
      Then the participant list includes each guest's dishes, quantities and subtotal, with the correct group total
      And "Ava" is listed as "unconfirmed" with 2 dishes
      And "Ben" is listed as "confirmed" with 1 dishes
      And "Casey" is listed as "unconfirmed" with 0 dishes
      When the guest "Ava" changes their cart by adding 1 dish
      And the owner reviews the participants
      Then the participant list includes each guest's dishes, quantities and subtotal, with the correct group total
      And the group total reflects the changed cart

    @api @known-defect
    # Homework explicitly requires []/0 before any guest joins. Current implementation persists and returns its empty Owner session.
    Scenario: A group with no joined guests has an empty participant list and zero total
      Given the owner has created a group pre-order
      When the owner reviews the participants
      Then the participant list is empty and the total is zero

    @api @negative
    Scenario: Another customer cannot review the group
      Given the owner has created a group pre-order
      When another customer reviews the participants
      Then only the owner can review the participants

    @api @negative
    Scenario: A guest cannot review the owner-only group list
      Given the owner has created a group pre-order
      And "Ava" has joined without adding dishes
      When a guest reviews the participants
      Then only the owner can review the participants

    @api @negative
    Scenario: A waiter cannot review the owner-only group list
      Given the owner has created a group pre-order
      When a waiter reviews the participants
      Then only the owner can review the participants

    @api @negative
    Scenario: An unauthenticated visitor cannot review the group
      Given the owner has created a group pre-order
      When an unauthenticated visitor reviews the participants
      Then only the owner can review the participants

    @api @negative
    Scenario: The participant list for a group that does not exist is not found
      Given the owner has created a group pre-order
      When the owner requests a group that does not exist
      Then the group is not found

    @ui @data-dependent
    Scenario: Owner reviews guest carts in the application
      Given the owner has created a group pre-order
      And "Lina" has added 2 dishes but has not confirmed the cart
      And "Noah" has added 1 dishes and confirmed the cart
      And "Casey" has joined without adding dishes
      When the owner signs in and opens the group review
      Then the owner sees each guest's dish details, quantity, subtotal and cart state on the review page
      And the empty guest cart is visibly empty with a zero subtotal
      And the consolidated total reflects submitted carts

    @ui @data-dependent @known-defect
    # Homework requires a total across all participants; the page's consolidated total excludes unsubmitted carts.
    Scenario: Owner sees a group total that includes unconfirmed guest carts
      Given the owner has created a group pre-order
      And "Ava" has added 2 dishes but has not confirmed the cart
      And "Ben" has added 1 dishes and confirmed the cart
      When the owner signs in and opens the group review
      Then the group total on the review page includes every guest cart

  Rule: Level 2 — submission includes only confirmed guest carts

    @api @data-dependent
    Scenario: Owner submits only confirmed carts with identical dishes consolidated
      Given the owner has created a group pre-order
      And "Ava" has added 2 dishes and confirmed the cart
      And "Ben" has added 3 dishes and confirmed the cart
      And "Casey" has added 4 dishes but has not confirmed the cart
      When the owner submits the group pre-order
      Then the group has been submitted
      And the waiter sees the submitted order with the confirmed guest composition

    @api @negative
    Scenario: Owner cannot submit when there are no confirmed guest carts
      Given the owner has created a group pre-order
      And "Ava" has added 1 dishes but has not confirmed the cart
      When the owner submits the group without any confirmed guest carts
      Then the group submission is refused because no guest carts were confirmed

    @api @negative
    Scenario: Submission is refused when the reservation order does not exist
      Given the owner has created a group pre-order
      When the owner submits the group with no reservation order
      Then the reservation order is reported missing

    @api @negative
    Scenario: Submission is refused when the reservation does not exist
      Given the owner has created a group pre-order
      When the owner submits the group without its reservation
      Then the reservation is reported missing

    @api @negative
    Scenario: Submission is refused when the group does not exist
      Given the owner has created a group pre-order
      When the owner submits a group that does not exist
      Then the group is not found

    @api @negative
    Scenario: A second submission is rejected
      Given the owner has created a group pre-order
      And "Ava" has added 1 dishes and confirmed the cart
      When the owner submits the group pre-order
      And the owner submits the already submitted group again
      Then the repeat submission is refused as already submitted

    @api @negative
    Scenario: A non-owner cannot submit the group
      Given the owner has created a group pre-order
      When a non-owner submits the group pre-order
      Then a non-owner is forbidden from submitting the group

    @api @wip @negative
    # The supported guest-cart API rejects submitting an empty cart, so this state cannot be prepared through the public contract.
    Scenario: Submission is refused when a confirmed cart has no dishes
      Given an otherwise confirmed guest cart contains no dishes
      When its owner attempts to submit the group
      Then the service explains that a confirmed cart has no dishes

    @api @known-defect
    # The Owner session exists, but guest-cart mutation accepts only its private session token; the Owner session has no token.
    Scenario: Owner can add dishes to their own group cart
      Given the owner has created a group pre-order
      When the owner adds a dish to their own group session
      Then the owner can add dishes to their own group cart

    @ui @data-dependent
    Scenario: Owner confirms the reviewed group pre-order
      Given the owner has created a group pre-order
      And the guest carts are ready for UI review
      When the owner signs in and opens the group review
      And the owner confirms the reviewed group pre-order
      Then the review page shows that the pre-order was sent to the restaurant

  Rule: Level 3 — owner removes a guest before the group order is submitted

    @api @data-dependent
    Scenario: Removing one guest leaves the other guest and recalculates the total
      Given the owner has created a group pre-order
      And "Removed" has added 2 dishes and confirmed the cart
      And "Kept" has added 1 dishes and confirmed the cart
      When the owner reviews the participants
      And the owner removes "Removed" from the group
      Then the removed guest is absent and does not affect the total
      And the group contains only "Kept" with the remaining subtotal

    @api @data-dependent
    Scenario: Owner can remove a guest who already confirmed their cart
      Given the owner has created a group pre-order
      And "Confirmed" has added 2 dishes and confirmed the cart
      When the owner removes "Confirmed" from the group
      Then the remaining group total is zero

    @api @data-dependent
    Scenario: Removing the last guest leaves an empty guest list and zero total
      Given the owner has created a group pre-order
      And "Last" has added 1 dishes but has not confirmed the cart
      When the owner removes the last guest
      Then the remaining group total is zero

    @api @negative
    Scenario: Owner cannot remove a guest who belongs to another group
      Given the owner has created a group pre-order
      When the owner tries to remove a guest from another group
      Then removal is rejected because the participant belongs to another group

    @api @negative
    Scenario: Owner cannot remove a participant who does not exist
      Given the owner has created a group pre-order
      When the owner tries to remove a participant who does not exist
      Then removal is rejected because the participant does not exist

    @api @negative
    Scenario: A non-owner cannot remove a participant
      Given the owner has created a group pre-order
      And "Ava" has joined without adding dishes
      When a non-owner tries to remove "Ava"
      Then a non-owner is forbidden from removing a participant

    @ui @data-dependent
    Scenario: Guest carts are reviewed, one is removed, and waiter receives only the remaining cart
      Given the owner has created a group pre-order
      And "Removed" has added 2 dishes and confirmed the cart
      And "Kept" has added 1 dishes and confirmed the cart
      When the owner signs in and opens the group review
      And the owner removes "Removed" from the review page
      Then the review page excludes the removed guest and their dishes
      When the owner confirms the reviewed group pre-order
      Then the review page shows that the pre-order was sent to the restaurant
      And the waiter sees the remaining guest's exact confirmed dish
