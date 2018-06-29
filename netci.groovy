// Groovy Script: http://www.groovy-lang.org/syntax.html
// Jenkins DSL: https://github.com/jenkinsci/job-dsl-plugin/wiki

import jobs.generation.Utilities;

static getJobName(def opsysName, def configName) {
  return "${opsysName}_${configName}"
}

static addArchival(def job, def filesToArchive, def filesToExclude) {
  def doNotFailIfNothingArchived = false
  def archiveOnlyIfSuccessful = false

  Utilities.addArchival(job, filesToArchive, filesToExclude, doNotFailIfNothingArchived, archiveOnlyIfSuccessful)
}

static addGithubPRTriggerForBranch(def job, def branchName, def jobName) {
  def prContext = "prtest/${jobName.replace('_', '/')}"
  def triggerPhrase = "(?i)^\\s*(@?dotnet-bot\\s+)?(re)?test\\s+(${prContext})(\\s+please)?\\s*\$"
  def triggerOnPhraseOnly = false

  Utilities.addGithubPRTriggerForBranch(job, branchName, prContext, triggerPhrase, triggerOnPhraseOnly)
}

static addXUnitDotNETResults(def job, def configName) {
  def resultFilePattern = "**/artifacts/${configName}/TestResults/*.xml"
  def skipIfNoTestFiles = false
    
  Utilities.addXUnitDotNETResults(job, resultFilePattern, skipIfNoTestFiles)
}

static addBuildSteps(def job, def projectName, def os, def configName, def isPR) {
  def buildJobName = getJobName(os, configName)
  def buildFullJobName = Utilities.getFullJobName(projectName, buildJobName, isPR)

  job.with {
    steps {
      if (os == "Windows_NT") {
        batchFile(""".\\eng\\CIBuild.cmd -configuration ${configName} -prepareMachine""")
      } else {
        shell("./eng/cibuild.sh --configuration ${configName} --prepareMachine")
      }
    }
  }
}

def platforms = [
  'Windows_NT:Release',
  'Windows_NT:Debug',
  'Ubuntu16.04:Release', 
  'Ubuntu16.04:Debug',
  'CentOS7.1:Debug',
  'Debian8.2:Debug',
  'RHEL7.2:x64:Debug',
  'OSX10.12:Debug',
]

def isPR = true

platforms.each { platform ->
  def (os, configName) = platform.tokenize(':')
       
  def projectName = GithubProject

  def branchName = GithubBranchName

  def filesToArchive = "**/artifacts/${configName}/**"

  def jobName = getJobName(os, configName)
  def fullJobName = Utilities.getFullJobName(projectName, jobName, isPR)
  def myJob = job(fullJobName)

  Utilities.standardJobSetup(myJob, projectName, isPR, "*/${branchName}")

  addGithubPRTriggerForBranch(myJob, branchName, jobName)      
  addArchival(myJob, filesToArchive, "")
  addXUnitDotNETResults(myJob, configName)
  
  Utilities.setMachineAffinity(myJob, os, 'latest-or-auto')  

  addBuildSteps(myJob, projectName, os, configName, isPR)
}
