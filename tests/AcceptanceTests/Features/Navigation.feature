Feature: Navigation
    In order to manage every part of a portfolio from one place
    As an authenticated user
    Each main section is reachable and presents its own heading

    Background:
        Given an authenticated user is signed in

    Scenario Outline: The <section> section presents its heading
        When the <section> section is opened
        Then the "<heading>" heading is displayed

        Examples:
            | section  | heading  |
            | holdings | Holdings |
            | accounts | Accounts |
            | activity | Activity |
            | goals    | Goals    |
            | calendar | Calendar |
            | settings | Settings |
