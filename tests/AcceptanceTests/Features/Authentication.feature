Feature: Authentication
    In order to keep a self-hosted portfolio private
    As the account holder
    Only the holder of valid credentials may reach the dashboard

    Rule: Valid credentials grant access to the portfolio

        Scenario: A registered user signs in with valid credentials
            Given the Capitrack application is open
            When credentials "admin" / "admin123" are submitted
            Then the portfolio dashboard is shown

    Rule: Invalid credentials are rejected at the sign-in screen

        Scenario Outline: Sign-in is refused for <case>
            Given the Capitrack application is open
            When credentials "<username>" / "<password>" are submitted
            Then access is denied and the sign-in screen remains

            Examples:
                | case               | username | password      |
                | a wrong password   | admin    | wrong-password |
                | an unknown user    | intruder | admin123       |
                | empty credentials  |          |               |
