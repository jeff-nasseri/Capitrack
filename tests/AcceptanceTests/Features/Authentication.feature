Feature: Authentication
    As a self-hosted user
    I want to sign in to Capitrack
    So that only I can see my portfolio

    Scenario: Signing in with valid credentials
        Given the Capitrack app is open
        When I sign in as "admin" with password "admin123"
        Then I should see the dashboard

    Scenario: Signing in with invalid credentials
        Given the Capitrack app is open
        When I sign in as "admin" with password "wrong-password"
        Then I should remain on the sign-in screen
