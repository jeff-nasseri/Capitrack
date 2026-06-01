Feature: Account creation
    In order to group holdings by where they are held
    As an authenticated user
    A newly created account becomes visible under Accounts

    Background:
        Given an authenticated user is signed in

    Scenario: A new account is created from a set of details
        When an account is created with the following details:
            | Field | Value     |
            | Name  | Brokerage |
        Then the account "Brokerage" is listed under Accounts

    Scenario Outline: Accounts of different kinds can be created
        When an account named "<name>" is created
        Then the account "<name>" is listed under Accounts

        Examples:
            | name             |
            | Savings          |
            | Pension          |
            | Crypto Wallet    |
