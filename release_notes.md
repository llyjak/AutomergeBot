## RELEASE NOTES

### VERSION 1.0.0 - 30th May 2018

**Features**
- automatic merging of branches according to provided merge directions
- automatic creation of temporary branches and pull requests in case merging has to be done manually
- notification service for informing people about need of manual merging
- automatic reloading of merge directions when changed - no need to stop the app in order for changes to take place
- preserving original author of changes in newly created pull requests


### VERSION 1.1.0 - 21st August 2018

**Features**
- automatic removal of temporary branches - upon closing pull request all temporary branches which are no longer needed are deleted from the remote repository
- notification service integrated with Slack for pinging people who do not merge their pull requests for a long time
- upon pushing new code to one of the monitored branches, automatic attempt to merge all pull requests targeting this branch, based on hope that newly added code resolved existing conflicts