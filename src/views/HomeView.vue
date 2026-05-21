<template>
  <div class="flex flex-col items-center justify-center p-8">
    <h2 class="text-2xl font-bold text-gray-800 mb-4">Home</h2>
    <p class="text-base text-gray-600 mb-6">Welcome to Flue - Vue to Flutter made easy.</p>

    <div class="flex flex-col gap-4 w-full max-w-sm">
      <div class="bg-white rounded-lg shadow-md p-4">
        <h3 class="text-lg font-semibold text-gray-700 mb-2">Features</h3>
        <ul class="flex flex-col gap-1">
          <li class="text-sm text-gray-600">Real-time Vue SFC to Dart compilation</li>
          <li class="text-sm text-gray-600">Tailwind CSS to Flutter decorations</li>
          <li class="text-sm text-gray-600">TypeScript to Dart logic bridge</li>
          <li class="text-sm text-gray-600">Hot reload with parallel compilation</li>
        </ul>
      </div>

      <button @click="router.push('/about')" class="bg-green-500 text-white px-4 py-2 rounded-lg font-medium">
        Go to About
      </button>
    </div>
  </div>
</template>

<script lang="ts">
const router = useRouter();
</script>
