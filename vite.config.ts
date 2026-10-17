import { defineConfig } from 'vite';

export default defineConfig({
  server: {
    watch: {
      // Ignorar a pasta de build do .NET/C++ e os dados temporários do WebView2
      ignored: [
        '**/native-browser/**',
        '**/sdk/**',
        '**/*.exe',
        '**/*.dll',
        '**/*.zip'
      ]
    }
  }
});
