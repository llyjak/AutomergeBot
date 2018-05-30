# AutomergeBot
AutomergeBot for GitHub

## What it is?
It is simple service which automates merging changes between branches using pull requests.
Applicable only to GitHub.

## How it works?
- AutomergeBot is attached to GitHub as a [webhook](https://developer.github.com/webhooks/).  
- AutomergeBot uses it's dedicated GitHub account to interact with [GitHub API](https://developer.github.com/v3/) (creating pull requests etc.)
- Merging directions are configurable

It reacts to events (currently only "push" event is handled).
If changes are pushed onto monitored branch it starts process of merging it to configured destination branch.  
AutomergeBot works in a context of 2 branches: source branch and branch to which changes should be merged.

### Merging process
1. Create temporary branch containing pushed changes
2. Create pull request from temporary branch to destination branch
3. Try to merge pull request
   - If there are conflicts notify changes author and provides detailed instructions what to do
   - If merged successfully remove temporary branch

## Problems it resolves


## Example application

1. Branches and merging strategy
  
   We have couple release branches representing production working code. Each change must be merged down respecting merging strategy:  
   R75 -> R75.1 -> R76.1 -> develop   
   R75 -> R76 -> R76.1 -> develop
   >Eg. changes pushed to R75 must go down to all release branches. But changes pushed to R76 goes only to R76.1 and develop
   
2. Release branches configuration
   - marked as "[protected](https://help.github.com/articles/about-protected-branches/)" in GitHub
   - marked as "Require pull request reviews before merging" 

3. We have "AutomergeBot" GitHub user with admin permissions to our repository which is used by AutomergeBot to interact with GitHub API

