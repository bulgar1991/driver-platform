// $localize for the code under test (each language build gets it from angular.json).
import '@angular/localize/init'
import { EnvironmentProviders, Provider } from '@angular/core'

const providers: (Provider | EnvironmentProviders)[] = []

export default providers
