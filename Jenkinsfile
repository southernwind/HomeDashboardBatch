node {
  stage('CheckOut'){
    checkout([$class: 'GitSCM', branches: [[name: '*/master']], doGenerateSubmoduleConfigurations: false, extensions: [[$class: 'SubmoduleOption', disableSubmodules: false, parentCredentials: false, recursiveSubmodules: true, reference: '', trackingSubmodules: false]], submoduleCfg: [], userRemoteConfigs: [[url: 'https://github.com/southernwind/HomeDashboardBatch']]])
  }

  stage('Configuration'){
    configFileProvider([configFile(fileId: '709bdcd4-af58-435d-8daf-cdadfc9c0cec', targetLocation: 'HomeDashboardBatch/appsettings.json')]) {}
  }

  stage('Build'){
    dotnetBuild configuration: 'Release', project: 'HomeDashboardBatch.sln', sdk: '.NET8', unstableIfWarnings: true
  }

  withCredentials( \
      bindings: [sshUserPrivateKey( \
        credentialsId: '1f4f3d15-c26b-4d2e-86a7-def517f966b3', \
        keyFileVariable: 'SSH_KEY', \
        usernameVariable: 'SSH_USER')]) {

    stage('Deploy'){
      sh 'scp -pr -i ${SSH_KEY} ./HomeDashboardBatch/bin/Release/net8.0/* ${SSH_USER}@batch-server.localnet:/opt/dashboard-batch'
      sh 'ssh -i ${SSH_KEY} ${SSH_USER}@batch-server.localnet chmod 755 /opt/dashboard-batch'
    }
  }

  stage('Notify Slack'){
    sh 'curl -X POST --data-urlencode "payload={\\"channel\\": \\"#jenkins-deploy\\", \\"username\\": \\"jenkins\\", \\"text\\": \\"ダッシュボード(Batch)のデプロイが完了しました。\\nBuild:${BUILD_URL}\\"}" ${WEBHOOK_URL}'
  }
}