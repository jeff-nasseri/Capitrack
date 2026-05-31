Feature: Navigation
    As a signed-in user
    I want to move between the main sections
    So that I can manage every part of my wealth

    Background:
        Given I am signed in

    Scenario Outline: Visiting the main sections
        When I navigate to "<path>"
        Then the page should show "<heading>"

        Examples:
            | path     | heading  |
            | holdings | Holdings |
            | accounts | Accounts |
            | activity | Activity |
            | goals    | Goals    |
            | calendar | Calendar |
            | settings | Settings |
