# Mertrellial

Adds Mercurial commit messages as comments on specified Trello cards

## Usage

`new Mertrellial("<repository directory filepath>", "<Trello appy key>", "<Trello auth token>").CheckCommits();`

You can set the `app key` and `auth token` in the constructor as default values so your instantiation is a little more clean. You can also specify a `DateTime` from which to check for commits (the default is within the last hour):

`new Mertrellial("<repo path>").CheckCommits(DateTime.Now.AddHours(-1));`

## Syntax

`<VERB> <BOARD> card <CARD #> commit message`

One message per line. The verb is optional. It will move the card to the specified list.

Example:

`coding Mertrellial card 2: updated README`

## Personalization

You'll want to personalize the `VERBS` Dictionary to match your Trello list names. You can also do this at runtime with the SetVerbs() method.

## Dependencies

[Trello.NET](https://github.com/dillenmeister/Trello.NET/)
[Mercurial.NET](https://mercurialnet.codeplex.com/)