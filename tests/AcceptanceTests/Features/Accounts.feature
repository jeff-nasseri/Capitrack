Feature: Accounts
    As a signed-in user
    I want to create an account
    So that I can group my holdings

    Background:
        Given I am signed in

    Scenario: Creating a new account
        When I create an account named "Brokerage"
        Then the account "Brokerage" should appear in my accounts
