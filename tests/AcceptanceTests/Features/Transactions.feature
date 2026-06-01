Feature: Transaction activity
    In order to review portfolio movements with confidence
    As an authenticated user
    The Activity section can be narrowed by transaction type and always reports the count

    Background:
        Given an authenticated user is signed in
        And the Activity section is open

    Scenario Outline: Activity can be filtered by the <type> transaction type
        When the activity is filtered by the "<type>" transaction type
        Then the transaction count is presented

        Examples:
            | type        |
            | buy         |
            | sell        |
            | dividend    |
            | transfer_in |
